using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public record GenericRpcType(Type ClrType,
        INamedRpcType TypeDefinition,
        IReadOnlyCollection<IRpcType> TypeArguments) : IRpcType;
}