using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RpcUnionType : RpcType
    {
        public IReadOnlyCollection<RpcType> Types { get; }

        public RpcUnionType(IReadOnlyCollection<RpcType> types, Type clrType) : base(clrType)
        {
            Types = types;
        }
    }
}