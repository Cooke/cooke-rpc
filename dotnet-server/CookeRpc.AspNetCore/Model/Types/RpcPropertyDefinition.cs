using System.Reflection;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public record RpcPropertyDefinition(string Name, IRpcType Type, MemberInfo ClrMemberInfo)
    {
        public bool IsOptional { get; init; }
    }
}
