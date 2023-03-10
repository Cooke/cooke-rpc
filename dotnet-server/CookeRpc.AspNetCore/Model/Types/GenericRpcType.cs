using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class GenericRpcType : IRpcType
    {
        public PrimitiveRpcType TypeDefinition { get; }

        public IReadOnlyCollection<IRpcType> TypeArguments { get; }

        public GenericRpcType(Type clrType, PrimitiveRpcType typeDefinition, IReadOnlyCollection<IRpcType> typeArguments)
        {
            ClrType = clrType;
            TypeDefinition = typeDefinition;
            TypeArguments = typeArguments;
        }
        public Type ClrType { get; }
    }
}