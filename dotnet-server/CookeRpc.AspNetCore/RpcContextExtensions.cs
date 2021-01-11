using System;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model;
using Microsoft.AspNetCore.Http;

namespace CookeRpc.AspNetCore
{
    public static class RpcContextExtensions
    {
        public static HttpContext GetHttpContext(this RpcContext context) =>
            (HttpContext) (context.Items[Constants.HttpContextKey] ?? throw new InvalidOperationException());
    }
}