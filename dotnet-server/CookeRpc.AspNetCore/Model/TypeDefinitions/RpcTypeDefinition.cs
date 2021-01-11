using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public abstract class RpcTypeDefinition 
    {
        protected RpcTypeDefinition(string name, Type clrType)
        {
            Name = name;
            ClrType = clrType;
        }

        public string Name { get; }

        public Type ClrType { get; set; }
    }
}