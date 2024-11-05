using System;

namespace CookeRpc.AspNetCore.Model.Types;

public interface IRpcType
{
    Type ClrType { get; }
}
