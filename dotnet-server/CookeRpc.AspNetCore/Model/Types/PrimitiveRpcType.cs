using System;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public record PrimitiveRpcType(string Name, Type ClrType) : INamedRpcType;
}