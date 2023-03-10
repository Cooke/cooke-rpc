using System;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RpcPrimitiveType : RpcType
    {
        public string Name { get; }
        
        public RpcPrimitiveType(string name, Type clrType) : base(clrType)
        {
            this.Name = name;
        }
    }
}