using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcObjectDefinition : RpcTypeDefinition
    {
        private readonly Func<RpcType?> _baseTypeThunk;

        public RpcObjectDefinition(string name,
            Type clrType,
            IReadOnlyCollection<RpcPropertyDefinition> properties,
            Func<RpcType?> baseTypeThunk,
            IReadOnlyCollection<RpcType> interfaces) : base(name, clrType)
        {
            _baseTypeThunk = baseTypeThunk;
            Properties = properties;
            Interfaces = interfaces;
        }

        public IReadOnlyCollection<RpcPropertyDefinition> Properties { get; }

        public IReadOnlyCollection<RpcType> Interfaces { get; }

        public RpcType? Base => _baseTypeThunk();
    }
}