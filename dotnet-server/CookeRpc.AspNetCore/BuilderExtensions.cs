using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CookeRpc.AspNetCore
{
    public static class BuilderExtensions
    {
        public static IServiceCollection AddRpc(this IServiceCollection services)
        {
            services.AddAuthorizationCore();
            return services;
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app, string path = "/rpc")
        {
            var model = new RpcModelBuilder(
                new RpcModelBuilderOptions { ContextType = typeof(HttpRpcContext) }
            );
            model.AddRpcServicesByAttribute();
            return UseRpc(app, model.Build(), path);
        }

        public static RpcModelBuilder AddRpcServicesByAttribute(this RpcModelBuilder model)
        {
            return model.AddRpcServicesByAttribute<RpcServiceAttribute>();
        }

        public static IRpcType AddType<T>(this RpcModelBuilder model)
        {
            return model.MapType(typeof(T));
        }

        public static RpcModelBuilder AddRpcServicesByAttribute<TAttribute>(
            this RpcModelBuilder model
        )
            where TAttribute : Attribute
        {
            var controllerTypes = Assembly
                .GetCallingAssembly()
                .GetTypes()
                .Concat(Assembly.GetEntryAssembly()?.GetTypes() ?? ArraySegment<Type>.Empty)
                .Where(x => x.GetCustomAttribute<TAttribute>() != null);

            foreach (var controllerType in controllerTypes)
            {
                model.AddService(controllerType);
            }

            return model;
        }

        public static IApplicationBuilder UseRpc(
            this IApplicationBuilder app,
            RpcModel model,
            string path = "/rpc"
        )
        {
            var serializerOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new OptionalRpcJsonConverterFactory(),
                    new JsonStringEnumConverter(),
                },
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        info =>
                            JsonTypeInfoModifiers.RpcPolymorphismJsonTypeInfoModifier(model, info)
                    }
                }
            };
            return UseRpc(app, model, serializerOptions, path);
        }

        public static IApplicationBuilder UseRpc(
            this IApplicationBuilder app,
            RpcModel model,
            JsonSerializerOptions serializerOptions,
            string path = "/rpc"
        )
        {
            UseRpcIntrospection(app, model, path + "/introspection");
            return app.UseMiddleware<RpcHttpMiddleware>(
                new RpcHttpMiddlewareOptions(model, serializerOptions) { Path = path }
            );
        }

        public static void UseRpcIntrospection(
            this IApplicationBuilder app,
            RpcModel model,
            string path = "/rpc/introspection"
        )
        {
            app.UseMiddleware<RpcIntrospectionHttpMiddleware>(
                new RpcIntrospectionHttpMiddlewareOptions(model) { Path = path }
            );
        }
    }
}
