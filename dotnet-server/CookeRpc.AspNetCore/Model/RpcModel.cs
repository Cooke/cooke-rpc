using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model;

public record RpcModel(
    IReadOnlyCollection<INamedRpcType> Types,
    IReadOnlyCollection<RpcServiceModel> Services
);
