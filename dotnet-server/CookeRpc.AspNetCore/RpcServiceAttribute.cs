using System;

namespace CookeRpc.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcServiceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false)]
    public class RpcTypeAttribute : Attribute
    {
        public string? Name { get; init; }

        public RpcTypeKind Kind { get; init; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class RegexRpcTypeAttribute : Attribute
    {
        public RegexRpcTypeAttribute(string pattern)
        {
            Pattern = pattern;
        }

        public string Pattern { get; init; }
    }

    public enum RpcTypeKind
    {
        Auto,
        Union,
        Primitive
    }
}