using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class UnionRpcType : IRpcType
    {
        public IReadOnlyCollection<IRpcType> Types { get; }

        public UnionRpcType(IReadOnlyCollection<IRpcType> types, Type clrType)
        {
            Types = types;
            ClrType = clrType;
        }
        public Type ClrType { get; }
    }
}