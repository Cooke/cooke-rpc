using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    public delegate object ParameterResolver(RpcContext context);

    public static class RpcDelegateFactory
    {
        private static readonly ParameterExpression ArgumentsParam = Expression.Parameter(
            typeof(object[])
        );

        public static (Lazy<RpcDelegate>, List<ParameterInfo>, Type returnType) Create(
            MethodInfo methodInfo,
            Type contextType,
            Func<ParameterInfo, ParameterResolver?>? customParameterResolver
        )
        {
            var controllerType = methodInfo.DeclaringType;
            if (controllerType == null)
            {
                throw new ArgumentException("Method info is not part of a controller type");
            }

            if (!contextType.IsAssignableTo(typeof(RpcContext)))
            {
                throw new ArgumentException(
                    $"Context type must be a subtype of {nameof(RpcContext)}"
                );
            }

            ParameterExpression contextParam = Expression.Parameter(typeof(RpcContext));
            var (rpcArguments, methodArguments) = CreateArguments(
                methodInfo,
                contextParam,
                contextType,
                ArgumentsParam,
                customParameterResolver
            );

            var spExpression = Expression.Property(
                contextParam,
                nameof(RpcContext.ServiceProvider)
            );
            var result = CallMethod(methodInfo, spExpression, controllerType, methodArguments);
            Type returnType = methodInfo.ReturnType;

            if (methodInfo.ReturnType == typeof(void))
            {
                result = Expression.Block(
                    result,
                    Expression.Constant(Task.FromResult<object?>(null))
                );
            }
            else if (methodInfo.ReturnType == typeof(Task))
            {
                returnType = typeof(void);
                var cmdCall = result;
                var task = Expression.Parameter(methodInfo.ReturnType);
                var continueWithMethod = typeof(Task)
                    .GetMethods()
                    .First(x =>
                        x.Name == "ContinueWith"
                        && x.ContainsGenericParameters
                        && x.GetParameters().Length == 1
                    )
                    .MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(
                    cmdCall,
                    continueWithMethod,
                    Expression.Lambda(
                        Expression.Block(
                            task.Call("GetAwaiter").Call("GetResult"),
                            Expression.Constant(null, typeof(object))
                        ),
                        task
                    )
                );
            }
            else if (methodInfo.ReturnType == typeof(ValueTask))
            {
                returnType = typeof(void);
                var cmdCall = result;
                var task = Expression.Parameter(methodInfo.ReturnType);

                var continueWithMethod = typeof(Task)
                    .GetMethods()
                    .First(x =>
                        x.Name == "ContinueWith"
                        && x.ContainsGenericParameters
                        && x.GetParameters().Length == 1
                    )
                    .MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(
                    Expression.Call(cmdCall, "AsTask", Type.EmptyTypes),
                    continueWithMethod,
                    Expression.Lambda(
                        Expression.Block(
                            task.Call("GetAwaiter").Call("GetResult"),
                            Expression.Constant(null, typeof(object))
                        ),
                        task
                    )
                );
            }
            else if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                returnType = methodInfo.ReturnType.GetTaskType()!;
                var cmdCall = result;
                var task = Expression.Parameter(methodInfo.ReturnType);

                var continueWithMethod = methodInfo
                    .ReturnType.GetMethods()
                    .First(x =>
                        x.Name == "ContinueWith"
                        && x.ContainsGenericParameters
                        && x.GetParameters().Length == 1
                    )
                    .MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(
                    cmdCall,
                    continueWithMethod,
                    Expression.Lambda(
                        task.Call("GetAwaiter").Call("GetResult").Convert<object>(),
                        task
                    )
                );
            }
            else if (
                methodInfo.ReturnType.IsGenericType
                && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)
            )
            {
                returnType = methodInfo.ReturnType.GetTaskType()!;
                var cmdCall = result;
                var taskType = typeof(Task<>).MakeGenericType(returnType);
                var task = Expression.Parameter(taskType);

                var continueWithMethod = taskType
                    .GetMethods()
                    .First(x =>
                        x.Name == "ContinueWith"
                        && x.ContainsGenericParameters
                        && x.GetParameters().Length == 1
                    )
                    .MakeGenericMethod(typeof(object));
                result = ExpressionExtensions.Call(
                    Expression.Call(cmdCall, "AsTask", Type.EmptyTypes),
                    continueWithMethod,
                    Expression.Lambda(
                        task.Call("GetAwaiter").Call("GetResult").Convert<object>(),
                        task
                    )
                );
            }
            else
            {
                var cmdCall = result;
                var fromResult = typeof(Task)
                    .GetMethod("FromResult")!
                    .MakeGenericMethod(typeof(object));
                result = Expression.Call(fromResult, Expression.Convert(cmdCall, typeof(object)));
            }

            var executeExpression = Expression.Lambda<Func<RpcContext, object?[], Task<object>>>(
                result,
                contextParam,
                ArgumentsParam
            );

            var executeLazy = new Lazy<RpcDelegate>(() =>
            {
                var execute = executeExpression.Compile();

                // Parameter conversion
                RpcDelegate execDelegate = async context =>
                {
                    var callArguments = new object?[rpcArguments.Count];
                    for (var i = 0; i < rpcArguments.Count; i++)
                    {
                        ParameterInfo parameterInfo = rpcArguments[i];
                        Optional<object?> argument;
                        try
                        {
                            argument = await context.Invocation.ConsumeArgument(
                                parameterInfo.ParameterType
                            );
                        }
                        catch (TargetInvocationException ex)
                            when (ex.InnerException is ArgumentException argumentException)
                        {
                            return new RpcError(
                                context.Invocation.Id,
                                Constants.ErrorCodes.BadRequest,
                                $"Invalid value for parameter '{parameterInfo.Name}': {argumentException.Message}",
                                ex
                            );
                        }
                        catch (ArgumentException ex)
                        {
                            return new RpcError(
                                context.Invocation.Id,
                                Constants.ErrorCodes.BadRequest,
                                $"Invalid value for parameter '{parameterInfo.Name}': {ex.Message}",
                                ex
                            );
                        }

                        if (!argument.HasValue)
                        {
                            if (!parameterInfo.HasDefaultValue)
                            {
                                return new RpcError(
                                    context.Invocation.Id,
                                    Constants.ErrorCodes.BadRequest,
                                    $"Missing parameter '{parameterInfo.Name}'",
                                    null
                                );
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
                        return new RpcReturnValue(
                            context.Invocation.Id,
                            new Optional<object?>(),
                            returnType
                        );
                    }

                    return new RpcReturnValue(
                        context.Invocation.Id,
                        new Optional<object?>(returnValue),
                        returnType
                    );
                };

                return CreateAuthorizeFunc(methodInfo, execDelegate);
            });

            return (executeLazy, rpcArguments, returnType);
        }

        private static Expression CallMethod(
            MethodInfo methodInfo,
            MemberExpression spExpression,
            Type controllerType,
            List<Expression> methodArguments
        )
        {
            var createControllerInstanceMethod = typeof(ActivatorUtilities)
                .GetMethods()
                .First(x => x.GetParameters().Length == 3);
            Expression instanceCreate = Expression.Call(
                null,
                createControllerInstanceMethod,
                spExpression,
                Expression.Constant(controllerType),
                Expression.Constant(Array.Empty<object>(), typeof(object[]))
            );
            Expression commandCall = Expression.Call(
                Expression.Convert(instanceCreate, controllerType),
                methodInfo,
                methodArguments
            );

            return commandCall;
        }

        private static (
            List<ParameterInfo> commandArguments,
            List<Expression> methodArguments
        ) CreateArguments(
            MethodInfo methodInfo,
            ParameterExpression contextParam,
            Type actualContextType,
            ParameterExpression argumentsArrayParam,
            Func<ParameterInfo, ParameterResolver?>? parameterResolver
        )
        {
            int inputIndex = 0;
            var outerArguments = new List<ParameterInfo>();
            var methodArguments = new List<Expression>();
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.ParameterType.IsAssignableTo(typeof(RpcContext)))
                {
                    if (!actualContextType.IsAssignableTo(parameterInfo.ParameterType))
                    {
                        throw new ArgumentException(
                            $"Parameter of type {parameterInfo.ParameterType.Name} is not compatible with context of type {actualContextType.Name} in method {methodInfo}"
                        );
                    }

                    methodArguments.Add(
                        Expression.Convert(contextParam, parameterInfo.ParameterType)
                    );
                    continue;
                }

                var resolver = parameterResolver?.Invoke(parameterInfo);
                if (resolver != null)
                {
                    methodArguments.Add(
                        Expression.Convert(
                            Expression.Invoke(Expression.Constant(resolver), contextParam),
                            parameterInfo.ParameterType
                        )
                    );
                    continue;
                }

                var inputArg = Expression.Convert(
                    Expression.ArrayIndex(argumentsArrayParam, Expression.Constant(inputIndex)),
                    parameterInfo.ParameterType
                );
                methodArguments.Add(inputArg);
                outerArguments.Add(parameterInfo);
                inputIndex++;
            }

            return (outerArguments, methodArguments);
        }

        private static RpcDelegate CreateAuthorizeFunc(MethodInfo methodInfo, RpcDelegate next)
        {
            if (methodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            {
                return next;
            }

            var authData = methodInfo
                .GetCustomAttributes<AuthorizeAttribute>()
                .Concat(methodInfo.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>())
                .Cast<IAuthorizeData>()
                .ToArray();

            if (!authData.Any())
            {
                return next;
            }

            return async context =>
            {
                var authResult = await IsAuthorized(context, authData);

                if (authResult.Failure != null)
                {
                    var errorCode =
                        context.User.Identity?.IsAuthenticated == true
                            ? Constants.ErrorCodes.NotAuthorized
                            : Constants.ErrorCodes.AuthenticationRequired;
                    return new RpcError(
                        context.Invocation.Id,
                        errorCode,
                        authResult.Failure.FailureReasons.Any()
                            ? string.Join(
                                ", ",
                                authResult.Failure.FailureReasons.Select(x => x.Message)
                            )
                            : errorCode == Constants.ErrorCodes.AuthenticationRequired
                                ? "Authentication required"
                                : "Not authorized",
                        null
                    );
                }

                return await next(context);
            };
        }

        private static async Task<AuthorizationResult> IsAuthorized(
            RpcContext context,
            IEnumerable<IAuthorizeData> authData
        )
        {
            var authorizationService =
                context.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var policyProvider =
                context.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authData);
            return await authorizationService.AuthorizeAsync(
                context.User,
                policy ?? throw new InvalidOperationException()
            );
        }
    }

    public static class Constants
    {
        public const string HttpContextKey = "HttpContext";

        public static class ErrorCodes
        {
            public const string AuthenticationRequired = "authentication_required";
            public const string NotAuthorized = "authorization_error";
            public const string ProcedureNotFound = "procedure_not_found";
            public const string BadRequest = "bad_request";
            public const string ServerError = "server_error";
        }
    }

    public static class ExpressionExtensions
    {
        public static MethodCallExpression Call(
            this Expression instance,
            MethodInfo method,
            params Expression[] args
        )
        {
            return Expression.Call(instance, method, args);
        }

        public static MethodCallExpression Call(
            this Expression instance,
            string methodName,
            params Expression[] args
        )
        {
            return Expression.Call(
                instance,
                instance.Type.GetMethod(methodName) ?? throw new InvalidOperationException(),
                args
            );
        }

        public static UnaryExpression Convert<T>(this Expression instance)
        {
            return Expression.Convert(instance, typeof(T));
        }
    }
}
