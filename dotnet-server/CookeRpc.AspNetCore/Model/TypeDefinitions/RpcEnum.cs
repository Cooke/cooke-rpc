using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcEnum : RpcType
    {
        public IReadOnlyCollection<RpcEnumMember> Members { get; }

        public RpcEnum(Type clrType, IReadOnlyCollection<RpcEnumMember> members) : base(clrType)
        {
            Members = members;
        }
    }
}