using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types;

public record ObjectRpcType(
    Type ClrType,
    IReadOnlyCollection<RpcPropertyDefinition> Properties,
    IReadOnlyCollection<IRpcType> Extends,
    bool IsAbstract,
    string Name,
    IReadOnlyCollection<TypeParameterRpcType> TypeParameters
) : INamedRpcType
{
    public IDictionary<String, Object> Metadata { get; init; } = new Dictionary<string, object>();
}
