using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class EnumRpcType : INamedRpcType
    {
        public Type ClrType { get; }
        public IReadOnlyCollection<RpcEnumMember> Members { get; }

        public EnumRpcType(string name, Type clrType, IReadOnlyCollection<RpcEnumMember> members)
        {
            Name = name;
            ClrType = clrType;
            Members = members;
        }
        public string Name { get; }
    }
}