using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CookeRpc.Tests;

public class RpcContextTests
{
    [Fact]
    public void Shall_Be_Able_To_Add_HttpRpcContext_Compatible_Controllers_To_RpcModel_For_HttpRpcContext()
    {
        var httpRpcModel = new RpcModelBuilder(new() { ContextType = typeof(HttpRpcContext) });
        httpRpcModel.AddService(typeof(HttpContextTestController));
        httpRpcModel.AddService(typeof(RpcContextTestController));
    }

    [Fact]
    public void Shall_Be_Able_To_Add_CustomRpcContext_Compatible_Controllers_To_RpcModel_For_CustomRpcContext()
    {
        var httpRpcModel = new RpcModelBuilder(new() { ContextType = typeof(CustomRpcContext) });
        httpRpcModel.AddService(typeof(CustomContextTestController));
        httpRpcModel.AddService(typeof(RpcContextTestController));
    }

    [Fact]
    public void Shall_Not_Be_Able_To_Add_HttpRpcContext_Compatible_Controllers_To_RpcModel_For_CustomRpcContext()
    {
        var httpRpcModel = new RpcModelBuilder(new() { ContextType = typeof(CustomRpcContext) });
        Assert.Throws<ArgumentException>(
            () => httpRpcModel.AddService(typeof(HttpContextTestController))
        );
    }

    [Fact]
    public void Shall_Be_Able_To_Add_CustomRpcContext_Compatible_Controllers_To_RpcModel_For_HttpRpcContext()
    {
        var httpRpcModel = new RpcModelBuilder(new() { ContextType = typeof(HttpRpcContext) });
        Assert.Throws<ArgumentException>(
            () => httpRpcModel.AddService(typeof(CustomContextTestController))
        );
    }

    [Fact]
    public void Shall_Be_Able_To_Use_Custom_Context()
    {
        var httpRpcModelBuilder = new RpcModelBuilder(
            new() { ContextType = typeof(CustomRpcContext) }
        );
        httpRpcModelBuilder.AddService(typeof(CustomContextTestController));
        httpRpcModelBuilder.AddService(typeof(RpcContextTestController));
        var httpRpcModel = httpRpcModelBuilder.Build();

        var service = httpRpcModel.Services.First(x => x.Name == "CustomContextTestController");
        var proc = service.Procedures.First();
        proc.Delegate.Invoke(
            new CustomRpcContext(
                new ServiceCollection().BuildServiceProvider(),
                CancellationToken.None,
                new ClaimsPrincipal(),
                new ReadOnlyDictionary<object, object?>(new Dictionary<object, object?>()),
                new RpcInvocation(
                    "hej",
                    service.Name,
                    proc.Name,
                    type => throw new InvalidOperationException()
                )
            )
        );
    }

    public class RpcContextTestController
    {
        public void OperationRpc(RpcContext context) { }
    }

    public class HttpContextTestController
    {
        public void OperationHttp(HttpRpcContext context) { }
    }

    public class CustomContextTestController
    {
        public void OperationCustom(CustomRpcContext context) { }
    }
}

public class CustomRpcContext : RpcContext
{
    public CustomRpcContext(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        ClaimsPrincipal user,
        ReadOnlyDictionary<object, object?> items,
        RpcInvocation invocation
    )
        : base(serviceProvider, cancellationToken, user, items, invocation) { }
}
