﻿using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcComplexDefinition : RpcTypeDefinition
    {
        public RpcComplexDefinition(string name,
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            IReadOnlyCollection<Types.RpcType> extenders) : base(name, clrType)
        {
            Properties = properties;
            Extenders = extenders;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }
        
        public IReadOnlyCollection<Types.RpcType> Extenders { get; }
    }
}