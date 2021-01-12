using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    // ReSharper disable once UnusedType.Global
    public class RpcScalarDefinition : RpcTypeDefinition
    {
        public RpcType ImplementationType { get; }

        public RpcScalarDefinition(string name, Type clrType, RpcType implementationType) : base(name, clrType)
        {
            ImplementationType = implementationType;
        }
    }
}