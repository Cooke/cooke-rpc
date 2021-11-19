using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcInterfaceDefinition : RpcTypeDefinition
    {
        public RpcInterfaceDefinition(string name,
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<RpcType> extends) : base(name, clrType)
        {
            Properties = properties;
            Extends = extends;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }
        
        public IReadOnlyCollection<RpcType> Extends { get; }
    }
}