using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class PrimitiveRpcType : INamedRpcType
    {
        public string Name { get; }

        public PrimitiveRpcType(string name, Type clrType)
        {
            Name = name;
            ClrType = clrType;
        }
        public Type ClrType { get; }
    }
}