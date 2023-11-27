using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CookeRpc.AspNetCore
{
    public class RpcHttpMiddlewareOptions
    {
        public RpcHttpMiddlewareOptions(RpcModel model, IRpcSerializer serializer)
        {
            Model = model;
            Serializer = serializer;
        }

        public string Path { get; init; } = "/rpc";

        public RpcModel Model { get; }

        public IRpcSerializer Serializer { get; }
    }

    public class RpcHttpMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RpcHttpMiddlewareOptions _options;
        private readonly IRpcSerializer _rpcSerializer;
        private readonly Dictionary<string, Dictionary<string, RpcProcedureModel>> _services;

        public RpcHttpMiddleware(RequestDelegate next, RpcHttpMiddlewareOptions options)
        {
            _next = next;
            _options = options;
            _rpcSerializer = options.Serializer;
            _services = options.Model.Services.ToDictionary(x => x.Name, x => x.Procedures.ToDictionary(y => y.Name));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(_options.Path))
            {
                await _next(context);
                return;
            }

            await ProcessRpcRequest(context);
        }

        private async Task ProcessRpcRequest(HttpContext context)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<RpcHttpMiddleware>>();
            var stopwatch = Stopwatch.StartNew();

            // Read everything
            ReadResult readResult;
            do
            {
                readResult = await context.Request.BodyReader.ReadAsync(context.RequestAborted);
                context.Request.BodyReader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
            } while (!readResult.IsCanceled && !readResult.IsCompleted);

            RpcInvocation invocation;
            try
            {
                invocation = _rpcSerializer.Parse(readResult.Buffer);
            }
            catch (Exception)
            {
                context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                return;
            }

            var rpcContext = new HttpRpcContext(context.RequestServices, context.RequestAborted, context.User,
                new(
                    new Dictionary<object, object?> {{Constants.HttpContextKey, context}}), invocation, context);

            using var _ = logger.BeginScope(new Dictionary<string, object?>
            {
                {"InvocationId", invocation.Id},
                {"Service", invocation.Service},
                {"Procedure", invocation.Procedure},
                {"IdentityName", rpcContext.User.Identity?.Name}
            });
            
            RpcResponse? response = null;
            try
            {
                response = await Dispatch(rpcContext, context, invocation);
            }
            finally
            {
                // Put this after to include serialization times in response logging
                switch (response)
                {
                    case RpcError rpcError:
                        logger.Log(LogLevel.Error, rpcError.Exception,
                            "RPC error ({RpcDuration} ms) {Service}.{Method}: {ErrorCode} ({ErrorMessage})",
                            stopwatch.ElapsedMilliseconds, rpcContext.Invocation.Service,
                            rpcContext.Invocation.Procedure, rpcError.Code, rpcError.Message);
                        break;

                    case RpcReturnValue:
                        logger.Log(LogLevel.Information, "RPC success ({RpcDuration} ms) {Service}.{Method}",
                            stopwatch.ElapsedMilliseconds, rpcContext.Invocation.Service,
                            rpcContext.Invocation.Procedure);
                        break;
                }
            }
        }

        private async Task<RpcResponse> Dispatch(RpcContext rpcContext, HttpContext httpContext, RpcInvocation invocation)
        {
            if (string.IsNullOrWhiteSpace(invocation.Procedure))
            {
                return Error(Constants.ErrorCodes.BadRequest, "Missing procedure");
            }

            if (!_services.TryGetValue(invocation.Service, out var procedures))
            {
                return Error(Constants.ErrorCodes.ProcedureNotFound, "No service with the give name");
            }

            if (!procedures.TryGetValue(invocation.Procedure, out var procedure))
            {
                return Error(Constants.ErrorCodes.ProcedureNotFound, "No procedure with the give name");
            }

            try
            {
                var response = await procedure.Delegate.Invoke(rpcContext);
                
                await httpContext.Request.BodyReader.CompleteAsync();

                _rpcSerializer.Serialize(response, httpContext.Response.BodyWriter, procedure.ReturnType.ClrType);

                return response;
            }
            catch (Exception e)
            {
                return Error(Constants.ErrorCodes.ServerError, "Unknown server error", e);
            }
            

            RpcResponse Error(string code, string message, Exception? exception = null)
            {
                return new RpcError(invocation.Id, code, message, exception);
            }
        }
    }
}