using System;
using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types;

public class NamedUnionRpcType : INamedRpcType
{
    public NamedUnionRpcType(String name, IReadOnlyCollection<IRpcType> types, Type clrType)
    {
        Name = name;
        Types = types;
        ClrType = clrType;
    }

    public Type ClrType { get; }

    public string Name { get; }

    public IReadOnlyCollection<IRpcType> Types { get; }
}
