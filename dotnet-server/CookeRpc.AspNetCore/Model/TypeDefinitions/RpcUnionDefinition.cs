using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcUnionDefinition : RpcTypeDefinition
    {
        public RpcUnionDefinition(string name, Type clrType, IReadOnlyCollection<Types.RpcType> types) : base(name, clrType)
        {
            Types = types;
        }

        public IReadOnlyCollection<Types.RpcType> Types { get; }
    }
}