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
            if (!context.Request.Path.StartsWithSegments(_options.Path)) {
                await _next(context);
                return;
            }

            await ProcessIntrospectionRequest(context);
        }

        private async Task ProcessIntrospectionRequest(HttpContext context)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                types = _model.TypesDefinitions.Select(x => (object) (x switch
                {
                    RpcEnumDefinition e => new
                    {
                        kind = "enum",
                        x.Name,
                        members = e.Members.Select(m => new
                        {
                            name = m.Name,
                            value = m.Value
                        })
                    },
                    RpcUnionDefinition union => new
                    {
                        kind = "union",
                        x.Name,
                        types = union.Types.Select(GetIntrospectionType)
                    },
                    RpcInterfaceDefinition inter => new
                    {
                        kind = "interface",
                        x.Name,
                        properties = inter.Properties.Count == 0 ? null : inter.Properties.Select(GetIntrospectionProperty),
                        interfaces = inter.Interfaces.Count == 0 ? null : inter.Interfaces.Select(GetIntrospectionType)
                    },
                    RpcObjectDefinition obj => new
                    {
                        kind = "object",
                        x.Name,
                        properties = obj.Properties.Count == 0 ? null : obj.Properties.Select(GetIntrospectionProperty),
                        interfaces = obj.Interfaces.Count == 0 ? null : obj.Interfaces.Select(GetIntrospectionType)
                    },
                    RpcNativeTypeDefinition => new
                    {
                        kind = "native",
                        x.Name
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
                        parameters = p.Parameters.Select(pa => new
                        {
                            pa.Name,
                            type = GetIntrospectionType(pa.Type)
                        })
                    })
                })
            }, _introspectionSerializerOptions);

            static object GetIntrospectionType(RpcType t) =>
                t switch
                {
                    UnionType unionType => new
                    {
                        kind = "union",
                        types = unionType.Types.Select(GetIntrospectionType)
                    },
                    GenericType genericType => new
                    {
                        kind = "generic",
                        name = genericType.Name,
                        typeArguments = genericType.TypeArguments.Select(GetIntrospectionType)
                    },
                    RefType {Name: { }} refType => refType.Name,
                    _ => throw new NotSupportedException()
                };

            static object GetIntrospectionProperty(RpcPropertyDefinition p) =>
                new
                {
                    p.Name,
                    Type = GetIntrospectionType(p.Type),
                    optional = (bool?) (p.IsOptional ? true : null)
                };
        }
    }
}