using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CookeRpc.AspNetCore;

public class RpcHttpMiddlewareOptions
{
    public RpcHttpMiddlewareOptions(RpcModel model, JsonSerializerOptions jsonSerializerOptions)
    {
        Model = model;
        JsonSerializerOptions = jsonSerializerOptions;
    }

    public string Path { get; init; } = "/rpc";

    public RpcModel Model { get; }

    public JsonSerializerOptions JsonSerializerOptions { get; }
}

public class RpcHttpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RpcHttpMiddlewareOptions _options;
    private readonly JsonRpcSerializer _rpcSerializer;
    private readonly Dictionary<string, Dictionary<string, RpcProcedureModel>> _services;

    public RpcHttpMiddleware(RequestDelegate next, RpcHttpMiddlewareOptions options)
    {
        _next = next;
        _options = options;
        _rpcSerializer = new JsonRpcSerializer(options.JsonSerializerOptions);
        _services = options.Model.Services.ToDictionary(
            x => x.Name,
            x => x.Procedures.ToDictionary(y => y.Name)
        );
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
        } while (readResult is { IsCanceled: false, IsCompleted: false });

        RpcInvocation invocation;
        try
        {
            invocation = _rpcSerializer.Parse(readResult.Buffer);
        }
        catch (Exception)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var rpcContext = new HttpRpcContext(
            context.RequestServices,
            context.RequestAborted,
            context.User,
            new(new Dictionary<object, object?> { { Constants.HttpContextKey, context } }),
            invocation,
            context
        );

        using var _ = logger.BeginScope(
            new Dictionary<string, object?>
            {
                { "InvocationId", invocation.Id },
                { "Service", invocation.Service },
                { "Procedure", invocation.Procedure },
                { "IdentityName", rpcContext.User.Identity?.Name }
            }
        );

        RpcResponse? response = null;
        try
        {
            response = await Dispatch(rpcContext, invocation);

            switch (response)
            {
                case RpcError rpcError:
                    switch (rpcError.Code)
                    {
                        case Constants.ErrorCodes.AuthenticationRequired:
                            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            break;
                        case Constants.ErrorCodes.NotAuthorized:
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            break;
                        case Constants.ErrorCodes.ServerError:
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            break;
                        default:
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            break;
                    }
                    break;
            }

            await context.Request.BodyReader.CompleteAsync();

            _rpcSerializer.Serialize(response, context.Response.BodyWriter);
        }
        finally
        {
            // Put this after to include serialization times in response logging
            switch (response)
            {
                case RpcError rpcError:
                    logger.Log(
                        LogLevel.Error,
                        rpcError.Exception,
                        "RPC error ({RpcDuration} ms) {Service}.{Method}: {ErrorCode} ({ErrorMessage})",
                        stopwatch.ElapsedMilliseconds,
                        rpcContext.Invocation.Service,
                        rpcContext.Invocation.Procedure,
                        rpcError.Code,
                        rpcError.Message
                    );
                    break;

                case RpcReturnValue:
                    logger.Log(
                        LogLevel.Information,
                        "RPC success ({RpcDuration} ms) {Service}.{Method}",
                        stopwatch.ElapsedMilliseconds,
                        rpcContext.Invocation.Service,
                        rpcContext.Invocation.Procedure
                    );
                    break;
            }
        }
    }

    private async Task<RpcResponse> Dispatch(RpcContext rpcContext, RpcInvocation invocation)
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
            return await procedure.Delegate.Invoke(rpcContext);
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
