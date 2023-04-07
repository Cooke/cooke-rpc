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
                types = _model.Types.Select(GetTypeDeclaration),
                services = _model.Services.Select(x => new
                {
                    x.Name,
                    procedures = x.Procedures.Select(p => new
                    {
                        p.Name,
                        returnType = GetTypeUsage(p.ReturnType),
                        parameters = p.Parameters.Select(pa => new
                        {
                            pa.Name,
                            type = GetTypeUsage(pa.Type)
                        })
                    })
                })
            }, _introspectionSerializerOptions);

            static object GetTypeUsage(IRpcType type) =>
                type switch
                {
                    INamedRpcType refType => refType.Name,
                    RegexRestrictedStringRpcType regexString => new
                    {
                        kind = "regex-restricted-string",
                        regex = regexString.Pattern
                    },
                    UnionRpcType unionType => new
                    {
                        kind = "union",
                        types = unionType.Types.Select(GetTypeUsage)
                    },
                    GenericRpcType genericType => new
                    {
                        kind = "generic",
                        name = genericType.TypeDefinition.Name,
                        typeArguments = genericType.TypeArguments.Select(GetTypeUsage)
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };

            static object GetTypeDeclaration(INamedRpcType type) =>
                type switch
                {
                    PrimitiveRpcType primType => new
                    {
                        primType.Name,
                        kind = "primitive",
                    },
                    EnumRpcType e => new
                    {
                        name = e.Name,
                        kind = "enum",
                        members = e.Members.Select(m => new
                        {
                            name = m.Name,
                            value = m.Value
                        })
                    },
                    ObjectRpcType obj => new
                    {
                        name = obj.Name,
                        kind = "object",
                        properties = obj.Properties.Count == 0 ? null : obj.Properties.Select(GetIntrospectionProperty),
                        extends = obj.Extends.Count == 0 ? null : obj.Extends.Select(GetTypeUsage),
                        @abstract = obj.IsAbstract
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };

            static object GetIntrospectionProperty(RpcPropertyDefinition p) =>
                new
                {
                    p.Name,
                    Type = GetTypeUsage(p.Type),
                    optional = (bool?)(p.IsOptional ? true : null)
                };
        }
    }
}