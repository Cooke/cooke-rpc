using System;

namespace CookeRpc.AspNetCore.Model.Types;

public record TypeParameterRpcType(string Name, Type ClrType) : INamedRpcType;
