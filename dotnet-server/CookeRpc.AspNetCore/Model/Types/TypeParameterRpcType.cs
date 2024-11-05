using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public record TypeParameterRpcType(string Name, Type ClrType) : INamedRpcType;
}
