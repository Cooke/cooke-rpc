using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
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
        private readonly IRpcSerializer _rpcSerializer;
        private readonly RpcModel _model;
        private readonly Dictionary<string, Dictionary<string, RpcProcedureModel>> _services;

        private readonly JsonSerializerOptions _introspectionSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public RpcHttpMiddleware(RequestDelegate next, RpcHttpMiddlewareOptions options)
        {
            _next = next;
            _rpcSerializer = options.Serializer;
            _model = options.Model;
            _services = options.Model.Services.ToDictionary(x => x.Name, x => x.Procedures.ToDictionary(y => y.Name));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/rpc"))
            {
                await _next(context);
                return;
            }

            if (context.Request.Path.Equals("/rpc/introspection"))
            {
                await ProcessIntrospectionRequest(context);
                return;
            }

            await ProcessRpcRequest(context);
        }

        private async Task ProcessRpcRequest(HttpContext context)
        {
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

            var rpcContext = new RpcContext(context.RequestServices, context.RequestAborted, context.User,
                new ReadOnlyDictionary<object, object?>(
                    new Dictionary<object, object?> {{Constants.HttpContextKey, context}}), invocation);

            var response = await Dispatch(rpcContext, invocation);

            await context.Request.BodyReader.CompleteAsync();

            var pipeWriter = PipeWriter.Create(context.Response.Body, new StreamPipeWriterOptions(leaveOpen: true));
            _rpcSerializer.Serialize(response, pipeWriter);
            await pipeWriter.FlushAsync();
            await pipeWriter.CompleteAsync();
        }

        private async Task ProcessIntrospectionRequest(HttpContext context)
        {
            static object GetIntrospectionType(RpcType t)
            {
                return t switch
                {
                    UnionType unionType => new
                    {
                        category = "union", types = unionType.Types.Select(GetIntrospectionType)
                    },
                    NativeType nativeType => new {category = "native", name = nativeType.Name},
                    GenericType genericType => new
                    {
                        name = genericType.Name,
                        category = "generic",
                        typeArguments = genericType.TypeArguments.Select(GetIntrospectionType)
                    },
                    _ => new {category = "custom", name = t.Name}
                };
            }

            await context.Response.WriteAsJsonAsync(new
            {
                types = _model.TypesDefinitions.Select(x => (object) (x switch
                {
                    RpcEnumDefinition e => new
                    {
                        category = "enum",
                        x.Name,
                        members = e.Members.Select(m => new {name = m.Name, value = m.Value})
                    },
                    RpcUnionDefinition union => new
                    {
                        category = "union", x.Name, types = union.Types.Select(GetIntrospectionType)
                    },
                    RpcComplexDefinition complex => new
                    {
                        category = "complex",
                        x.Name,
                        properties =
                            complex.Properties.Select(p => new
                            {
                                p.Name,
                                Type = GetIntrospectionType(p.Type),
                                optiona = (bool?) (p.IsOptional ? true : null)
                            }),
                        extenders = complex.Extenders.Any() ? complex.Extenders.Select(GetIntrospectionType) : null
                    },
                    RpcScalarDefinition scalar => new
                    {
                        category = "scalar",
                        x.Name,
                        ImplementationType = GetIntrospectionType(scalar.ImplementationType)
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(x))
                })),
                services = _model.Services.Select(x => new
                {
                    x.Name,
                    procedures = x.Procedures.Select(p => new
                    {
                        p.Name,
                        returnType = GetIntrospectionType(p.ReturnType),
                        parameters = p.Parameters.Select(pa =>
                            new {pa.Name, type = GetIntrospectionType(pa.Type)})
                    })
                })
            }, _introspectionSerializerOptions);
        }

        private async Task<RpcResponse> Dispatch(RpcContext rpcContext, RpcInvocation invocation)
        {
            var logger = rpcContext.ServiceProvider.GetRequiredService<ILogger<RpcHttpMiddleware>>();
            var stopwatch = Stopwatch.StartNew();
            using var _ =
                logger.BeginScope(new {InvocationId = invocation.Id, invocation.Service, invocation.Procedure});

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
                var rpcResponse = await procedure.Delegate.Invoke(rpcContext);
                logger.LogInformation("RPC success ({ExecutionTime} ms) {Service}.{Method}", stopwatch.Elapsed.TotalMilliseconds,
                    rpcContext.Invocation.Service, rpcContext.Invocation.Procedure);
                return rpcResponse;
            }
            catch (Exception e)
            {
                return Error(Constants.ErrorCodes.ServerError, "Unknown server error", e);
            }

            RpcResponse Error(string code, string message, Exception? exception = null)
            {
                logger.LogError(exception, "RPC error ({ExecutionTime} ms): {Code} ({Message})", stopwatch.Elapsed.TotalMilliseconds, code,
                    message);
                return new RpcError(invocation.Id, code, message);
            }
        }
    }
}