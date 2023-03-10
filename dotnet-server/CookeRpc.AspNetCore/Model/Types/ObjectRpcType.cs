using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class ObjectRpcType : INamedRpcType
    {
        public ObjectRpcType(
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<IRpcType> extends,
            bool isAbstract, string name)
        {
            ClrType = clrType;
            Properties = properties;
            Extends = extends;
            IsAbstract = isAbstract;
            Name = name;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }

        public IReadOnlyCollection<IRpcType> Extends { get; }

        public bool IsAbstract { get; }
        public Type ClrType { get; }
        public string Name { get; }
    }
}