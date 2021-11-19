﻿using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcObjectDefinition : RpcTypeDefinition
    {
        public RpcObjectDefinition(string name,
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<RpcType> implements) : base(name, clrType)
        {
            Properties = properties;
            Implements = implements;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }

        public IReadOnlyCollection<RpcType> Implements { get; }
    }
}