using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RpcUnionType : RpcType
    {
        public IReadOnlyCollection<RpcType> Types { get; }

        public RpcUnionType(IReadOnlyCollection<RpcType> types)
        {
            Types = types;
        }

        public override string? Name { get; } = null;
    }
}