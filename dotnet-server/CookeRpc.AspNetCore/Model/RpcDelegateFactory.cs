using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CookeRpc.AspNetCore.Model
{
    public static class RpcDelegateFactory
    {
        private static readonly ParameterExpression ContextParam = Expression.Parameter(typeof(RpcContext));
        private static readonly ParameterExpression ArgumentsParam = Expression.Parameter(typeof(object[]));

        private static readonly IDictionary<Type, Expression> ContextArguments = new Dictionary<Type, Expression>
        {
            {typeof(RpcContext), ContextParam}
        };

        public static (RpcDelegate, List<ParameterInfo>, Type returnType) Create(MethodInfo methodInfo)
        {
            var controllerType = methodInfo.DeclaringType;
            if (controllerType == null)
            {
                throw new ArgumentException("Method info is not part of a controller type");
            }

            var (rpcArguments, methodArguments) = CreateArguments(methodInfo, ContextArguments, ArgumentsParam);

            var spExpression = Expression.Property(ContextParam, nameof(RpcContext.ServiceProvider));
            var result = CallMethod(methodInfo, spExpression, controllerType, methodArguments);
            Type returnType = methodInfo.ReturnType;

            if (methodInfo.ReturnType == typeof(void))
            {
                result = Expression.Block(result, Expression.Constant(Task.FromResult<object?>(null)));
            }
            else if (methodInfo.ReturnType == typeof(Task))
            {
                returnType = typeof(void);
                var cmdCall = result;
                var task = Expression.Parameter(methodInfo.ReturnType);
                var continueWithMethod = typeof(Task).GetMethods()
                    .First(x => x.Name == "ContinueWith" && x.ContainsGenericParameters &&
                                x.GetParameters().Length == 1).MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(cmdCall, continueWithMethod,
                    Expression.Lambda(
                        Expression.Block(task.Call("GetAwaiter").Call("GetResult"),
                            Expression.Constant(null, typeof(object))), task));
            }
            else if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                returnType = methodInfo.ReturnType.GetTaskType()!;
                var cmdCall = result;
                var task = Expression.Parameter(methodInfo.ReturnType);

                var continueWithMethod = methodInfo.ReturnType.GetMethods()
                    .First(x => x.Name == "ContinueWith" && x.ContainsGenericParameters &&
                                x.GetParameters().Length == 1).MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(cmdCall, continueWithMethod,
                    Expression.Lambda(task.Call("GetAwaiter").Call("GetResult").Convert<object>(), task));
            }
            else
            {
                var cmdCall = result;
                var fromResult = typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(typeof(object));
                result = Expression.Call(fromResult, cmdCall);
            }

            var executeExpression =
                Expression.Lambda<Func<RpcContext, object?[], Task<object>>>(result, ContextParam, ArgumentsParam);

            var execute = executeExpression.Compile();

            // Parameter conversion
            RpcDelegate execDelegate = async context =>
            {
                var callArguments = new object?[rpcArguments.Count];
                for (var i = 0; i < rpcArguments.Count; i++)
                {
                    ParameterInfo parameterInfo = rpcArguments[i];
                    var argument = await context.Invocation.ConsumeArgument(parameterInfo.ParameterType);
                    if (!argument.HasValue)
                    {
                        if (!parameterInfo.HasDefaultValue)
                        {
                            throw new RpcInvocationException($"Missing parameter '{parameterInfo.Name}'");
                        }

                        callArguments[i] = parameterInfo.DefaultValue;
                    }
                    else
                    {
                        callArguments[i] = argument.Value;
                    }
                }

                var returnValue = await execute(context, callArguments);
                if (returnType == typeof(void))
                {
                    return new RpcReturnValue(context.Invocation.Id, new Optional<object?>());
                }

                return new RpcReturnValue(context.Invocation.Id, new Optional<object?>(returnValue));
            };

            // Authorization
            RpcDelegate authDelegate = async context =>
            {
                var authorized = await AuthorizeGuard(methodInfo, context.ServiceProvider, context.User);
                if (!authorized)
                {
                    return new RpcError(context.Invocation.Id, Constants.ErrorCodes.AuthorizationError,
                        "Not authorized", null);
                }

                return await execDelegate(context);
            };

            return (authDelegate, rpcArguments, returnType);
        }

        private static Expression CallMethod(MethodInfo methodInfo,
            MemberExpression spExpression,
            Type controllerType,
            List<Expression> methodArguments)
        {
            var createControllerInstanceMethod =
                typeof(ActivatorUtilities).GetMethods().First(x => x.GetParameters().Length == 3);
            Expression instanceCreate = Expression.Call(null, createControllerInstanceMethod, spExpression,
                Expression.Constant(controllerType), Expression.Constant(Array.Empty<object>(), typeof(object[])));
            Expression commandCall = Expression.Call(Expression.Convert(instanceCreate, controllerType), methodInfo,
                methodArguments);


            return commandCall;
        }

        private static (List<ParameterInfo> commandArguments, List<Expression> methodArguments) CreateArguments(
            MethodInfo methodInfo,
            IDictionary<Type, Expression> contextArguments,
            ParameterExpression argumentsArrayParam)
        {
            int inputIndex = 0;
            var outerArguments = new List<ParameterInfo>();
            var methodArguments = new List<Expression>();
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (contextArguments.TryGetValue(parameterInfo.ParameterType, out var arg))
                {
                    methodArguments.Add(arg);
                }
                else
                {
                    var inputArg =
                        Expression.Convert(Expression.ArrayIndex(argumentsArrayParam, Expression.Constant(inputIndex)),
                            parameterInfo.ParameterType);
                    methodArguments.Add(inputArg);
                    outerArguments.Add(parameterInfo);
                    inputIndex++;
                }
            }

            return (outerArguments, methodArguments);
        }

        private static async Task<bool> AuthorizeGuard(MethodInfo methodInfo, IServiceProvider sp, ClaimsPrincipal user)
        {
            if (methodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            {
                return true;
            }
            
            var authData = methodInfo.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(methodInfo.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>()).Cast<IAuthorizeData>();

            if (!authData.Any())
            {
                return true;
            }

            var authorizationService = sp.GetRequiredService<IAuthorizationService>();
            var policyProvider = sp.GetRequiredService<IAuthorizationPolicyProvider>();
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authData);
            var authResult =
                await authorizationService.AuthorizeAsync(user, policy ?? throw new InvalidOperationException());

            return authResult.Succeeded;
        }
    }

    public static class Constants
    {
        public const string HttpContextKey = "HttpContext";

        public static class ErrorCodes
        {
            public const string AuthorizationError = "authorization_error";
            public const string ParseError = "parse_error";
            public const string ProcedureNotFound = "procedure_not_found";
            public const string BadRequest = "bad_request";
            public const string ServerError = "server_error";
        }
    }

    public class RpcInvocationException : Exception
    {
        public RpcInvocationException(string? message) : base(message)
        {
        }

        public RpcInvocationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }

    public static class ExpressionExtensions
    {
        public static MethodCallExpression Call(this Expression instance, MethodInfo method, params Expression[] args)
        {
            return Expression.Call(instance, method, args);
        }

        public static MethodCallExpression Call(this Expression instance, string methodName, params Expression[] args)
        {
            return Expression.Call(instance,
                instance.Type.GetMethod(methodName) ?? throw new InvalidOperationException(), args);
        }

        public static UnaryExpression Convert<T>(this Expression instance)
        {
            return Expression.Convert(instance, typeof(T));
        }
    }
}