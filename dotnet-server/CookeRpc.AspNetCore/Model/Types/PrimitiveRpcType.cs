using System;

namespace CookeRpc.AspNetCore.Model.Types;

public record PrimitiveRpcType(string Name, Type ClrType) : INamedRpcType;
