using System;

namespace CookeRpc.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcServiceAttribute : Attribute { }

    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum,
        Inherited = false
    )]
    public class RpcTypeAttribute : Attribute
    {
        public string? Name { get; init; }

        public RpcTypeKind Kind { get; init; }
    }

    public enum RpcTypeKind
    {
        Auto,
        Union,
        Primitive
    }
}
