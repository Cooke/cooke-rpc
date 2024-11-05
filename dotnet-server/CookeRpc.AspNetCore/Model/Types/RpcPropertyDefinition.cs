using System.Reflection;

namespace CookeRpc.AspNetCore.Model.Types;

public record RpcPropertyDefinition(string Name, IRpcType Type, MemberInfo ClrMemberInfo)
{
    public bool IsOptional { get; init; }
}
