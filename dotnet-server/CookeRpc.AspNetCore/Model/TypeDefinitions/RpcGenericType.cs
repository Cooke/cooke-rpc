using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RpcGenericType : RpcType
    {
        public RpcPrimitiveType TypeDefinition { get; }

        public IReadOnlyCollection<RpcType> TypeArguments { get; }

        public RpcGenericType(Type clrType, RpcPrimitiveType typeDefinition, IReadOnlyCollection<RpcType> typeArguments) :
            base(clrType)
        {
            TypeDefinition = typeDefinition;
            TypeArguments = typeArguments;
        }
    }
}