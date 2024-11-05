using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public record ObjectRpcType(
        Type ClrType,
        IReadOnlyCollection<RpcPropertyDefinition> Properties,
        IReadOnlyCollection<IRpcType> Extends,
        bool IsAbstract,
        string Name,
        IReadOnlyCollection<TypeParameterRpcType> TypeParameters
    ) : INamedRpcType
    {
        public IDictionary<String, Object> Metadata { get; init; } =
            new Dictionary<string, object>();
    }
}
