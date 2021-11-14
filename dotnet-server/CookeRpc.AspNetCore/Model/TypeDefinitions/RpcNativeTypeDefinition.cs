using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RpcNativeTypeDefinition : RpcTypeDefinition
    {
        public RpcNativeTypeDefinition(string name, Type clrType) : base(name, clrType)
        {
        }
    }
}