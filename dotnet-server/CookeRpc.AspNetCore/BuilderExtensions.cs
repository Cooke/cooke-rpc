using System.Linq;
using System.Reflection;
using CookeRpc.AspNetCore.JsonSerialization;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CookeRpc.AspNetCore
{
    public static class BuilderExtensions
    {
        public static IServiceCollection AddRpc(this IServiceCollection services)
        {
            return services;
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app, string path = "/rpc")
        {
            var controllerTypes = Assembly.GetCallingAssembly().GetTypes()
                .Where(x => x.GetCustomAttribute<RpcServiceAttribute>() != null);

            var model = new RpcModel(new RpcModelOptions());
            foreach (var controllerType in controllerTypes)
            {
                model.AddService(controllerType);
            }

            return UseRpc(app, model, path);
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app, RpcModel model, string path = "/rpc")
        {
            var serializer = new JsonRpcSerializer(new RpcModelTypeBinder(model));
            return UseRpc(app, model, serializer, path);
        }

        public static IApplicationBuilder UseRpc(this IApplicationBuilder app,
            RpcModel model,
            JsonRpcSerializer serializer,
            string path = "/rpc")
        {
            return app.UseMiddleware<RpcHttpMiddleware>(new RpcHttpMiddlewareOptions(model, serializer) {Path = path});
        }
    }
}