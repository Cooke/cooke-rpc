using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using Microsoft.AspNetCore.Http;

namespace CookeRpc.AspNetCore
{
    public class RpcIntrospectionHttpMiddlewareOptions
    {
        public RpcIntrospectionHttpMiddlewareOptions(RpcModel model)
        {
            Model = model;
        }

        public string Path { get; init; } = "/rpc/introspection";

        public RpcModel Model { get; }
    }

    public class RpcIntrospectionHttpMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RpcIntrospectionHttpMiddlewareOptions _options;
        private readonly RpcModel _model;

        private readonly JsonSerializerOptions _introspectionSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public RpcIntrospectionHttpMiddleware(RequestDelegate next, RpcIntrospectionHttpMiddlewareOptions options)
        {
            _next = next;
            _options = options;
            _model = options.Model;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(_options.Path))
            {
                await _next(context);
                return;
            }

            await ProcessIntrospectionRequest(context);
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
                                optional = (bool?) (p.IsOptional ? true : null)
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
    }
}