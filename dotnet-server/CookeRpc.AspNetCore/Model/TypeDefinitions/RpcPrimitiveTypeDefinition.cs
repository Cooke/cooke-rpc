using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcPrimitiveTypeDefinition : RpcTypeDefinition
    {
        public RpcPrimitiveTypeDefinition(string name, Type clrType) : base(name, clrType)
        {
        }
    }
}