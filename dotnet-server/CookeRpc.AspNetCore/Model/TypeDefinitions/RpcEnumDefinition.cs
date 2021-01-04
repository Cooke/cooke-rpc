using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcEnumDefinition : RpcTypeDefinition
    {
        public IReadOnlyCollection<RpcEnumMember> Members { get; }

        public RpcEnumDefinition(string name, Type clrType, IReadOnlyCollection<RpcEnumMember> members) : base(
            name, clrType)
        {
            Members = members;
        }
    }
}