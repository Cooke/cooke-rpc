using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public interface IRpcType
    {
        Type ClrType { get; }
    }

}