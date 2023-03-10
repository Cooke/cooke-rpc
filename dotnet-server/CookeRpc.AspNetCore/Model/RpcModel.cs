using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model
{
    public record RpcModel(IReadOnlyCollection<INamedRpcType> Types,
        IReadOnlyCollection<RpcServiceModel> Services);

}