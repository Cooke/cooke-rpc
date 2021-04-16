using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace CookeRpc.AspNetCore.Core
{
    public class RpcContext
    {
        public RpcContext(IServiceProvider serviceProvider,
            CancellationToken cancellationToken,
            ClaimsPrincipal user,
            IReadOnlyDictionary<object, object?> items,
            RpcInvocation invocation)
        {
            ServiceProvider = serviceProvider;
            CancellationToken = cancellationToken;
            User = user;
            Items = items;
            Invocation = invocation;
        }

        public IServiceProvider ServiceProvider { get; init; }

        public CancellationToken CancellationToken { get; init; }

        public ClaimsPrincipal User { get; init; }

        public IReadOnlyDictionary<object, object?> Items { get; }

        public RpcInvocation Invocation { get; init; }
    }

    public class HttpRpcContext : RpcContext
    {
        public HttpContext HttpContext { get; }

        public HttpRpcContext(IServiceProvider serviceProvider,
            CancellationToken cancellationToken,
            ClaimsPrincipal user,
            ReadOnlyDictionary<object, object?> items,
            RpcInvocation invocation,
            HttpContext httpContext) : base(serviceProvider, cancellationToken, user, items, invocation)
        {
            HttpContext = httpContext;
        }
    }
}