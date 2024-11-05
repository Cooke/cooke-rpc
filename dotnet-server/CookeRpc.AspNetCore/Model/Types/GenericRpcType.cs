using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types;

public record GenericRpcType(
    Type ClrType,
    INamedRpcType TypeDefinition,
    IReadOnlyCollection<IRpcType> TypeArguments
) : IRpcType;
