using System;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Threading;

namespace CookeRpc.AspNetCore.Core
{
    public class RpcContext
    {
        public RpcContext(IServiceProvider serviceProvider,
            CancellationToken cancellationToken,
            ClaimsPrincipal user,
            ReadOnlyDictionary<object, object?> items,
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

        public ReadOnlyDictionary<object, object?> Items { get; }

        public RpcInvocation Invocation { get; init; }
    }
}