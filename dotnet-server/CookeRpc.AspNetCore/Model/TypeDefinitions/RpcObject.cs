using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcObject : RpcType
    {
        public RpcObject(
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<RpcType> extends) : base(clrType)
        {
            Properties = properties;
            Extends = extends;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }

        public IReadOnlyCollection<RpcType> Extends { get; }
    }
}