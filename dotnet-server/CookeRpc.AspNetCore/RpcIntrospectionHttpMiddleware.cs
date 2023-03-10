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
                typeDeclarations = _model.TypeDeclarations
                    .Select(dec => new
                    {
                        dec.Name,
                        Type = GetIntrospectionType(dec.Type)
                    }),
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

            static object GetIntrospectionType(RpcType type) =>
                type switch
                {
                    RpcPrimitiveType primType => new
                    {
                        kind = "primitive",
                        primType.Name
                    },
                    RpcRefType refType => refType.ReferencedType.Name,
                    RpcUnionType unionType => new
                    {
                        kind = "union",
                        types = unionType.Types.Select(GetIntrospectionType)
                    },
                    RpcGenericType genericType => new
                    {
                        kind = "generic",
                        name = genericType.TypeDefinition.Name,
                        typeArguments = genericType.TypeArguments.Select(GetIntrospectionType)
                    },
                    RpcEnum e => new
                    {
                        kind = "enum",
                        members = e.Members.Select(m => new
                        {
                            name = m.Name,
                            value = m.Value
                        })
                    },
                    RpcObject obj => new
                    {
                        kind = "object",
                        properties = obj.Properties.Count == 0 ? null : obj.Properties.Select(GetIntrospectionProperty),
                        extends = obj.Extends.Count == 0 ? null : obj.Extends.Select(GetIntrospectionType)
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };

            static object GetIntrospectionProperty(RpcPropertyDefinition p) =>
                new
                {
                    p.Name,
                    Type = GetIntrospectionType(p.Type),
                    optional = (bool?)(p.IsOptional ? true : null)
                };
        }
    }
}