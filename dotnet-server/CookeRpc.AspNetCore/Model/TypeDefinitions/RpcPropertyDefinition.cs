using System.Reflection;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcPropertyDefinition
    {
        public RpcPropertyDefinition(string name, RpcType type, MemberInfo clrMemberInfo)
        {
            Name = name;
            Type = type;
            ClrMemberInfo = clrMemberInfo;
        }

        public string Name { get; }

        public bool IsOptional { get; init; }

        public RpcType Type { get; }

        // May be null if no corresponding clr member info exists
        public MemberInfo ClrMemberInfo { get; }
    }
}