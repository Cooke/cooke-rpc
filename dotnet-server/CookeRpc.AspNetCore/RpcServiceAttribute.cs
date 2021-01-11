using System;

namespace CookeRpc.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcServiceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum)]
    public class RpcType : Attribute
    {
        public string? Name { get; init; }
    }
}