using System;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public record RestrictedRpcType(IRpcType InnerType, string Restriction) : IRpcType
    {
        public Type ClrType => InnerType.ClrType;
    }
}