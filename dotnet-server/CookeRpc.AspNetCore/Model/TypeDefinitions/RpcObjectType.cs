using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcObjectType : RpcType
    {
        public RpcObjectType(
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<RpcType> extends,
            bool isAbstract) : base(clrType)
        {
            Properties = properties;
            Extends = extends;
            IsAbstract = isAbstract;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }

        public IReadOnlyCollection<RpcType> Extends { get; }
        
        public bool IsAbstract { get; }
    }
}