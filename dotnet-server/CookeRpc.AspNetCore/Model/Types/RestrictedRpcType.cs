using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public class RestrictedRpcType : IRpcType
    {
        public RestrictedRpcType(string name, Type clrType, string restriction)
        {
            ClrType = clrType;
        }

        public Type ClrType { get; }
    }
}