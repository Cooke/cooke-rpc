using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
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
            var model = new RpcModelBuilder(new RpcModelBuilderOptions { ContextType = typeof(HttpRpcContext) });
            model.AddRpcServicesByAttribute();
            return UseRpc(app, model.Build(), path);
        }

        public static RpcModelBuilder AddRpcServicesByAttribute(this RpcModelBuilder model)
        {
            return model.AddRpcServicesByAttribute<RpcServiceAttribute>();
        }

        public static RpcType AddType<T>(this RpcModelBuilder model)
        {
            return model.AddType(typeof(T));
        }

        // public static RpcType AddType<T>(this RpcModelBuilder model, RpcType rpcType)
        // {
        //     return model.AddType(typeof(T), rpcType);
        // }

        public static RpcModelBuilder AddRpcServicesByAttribute<TAttribute>(this RpcModelBuilder model) where TAttribute : Attribute
        {
            var controllerTypes = Assembly.GetCallingAssembly().GetTypes()
                .Concat(Assembly.GetEntryAssembly()?.GetTypes() ?? ArraySegment<Type>.Empty)
                .Where(x => x.GetCustomAttribute<TAttribute>() != null);

            foreach (var controllerType in controllerTypes) {
                model.AddService(controllerType);
            }

            return model;
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app, RpcModel model, string path = "/rpc")
        {
            var serializer = new SystemTextJsonRpcSerializer(new RpcModelTypeBinder(model));
            return UseRpc(app, model, serializer, path);
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app,
            RpcModel model,
            IRpcSerializer serializer,
            string path = "/rpc")
        {
            UseRpcIntrospection(app, model, path + "/introspection");
            return app.UseMiddleware<RpcHttpMiddleware>(
                new RpcHttpMiddlewareOptions(model, serializer) { Path = path });
        }

        public static void UseRpcIntrospection(this IApplicationBuilder app,
            RpcModel model,
            string path = "/rpc/introspection")
        {
            app.UseMiddleware<RpcIntrospectionHttpMiddleware>(
                new RpcIntrospectionHttpMiddlewareOptions(model) { Path = path });
        }
    }
}