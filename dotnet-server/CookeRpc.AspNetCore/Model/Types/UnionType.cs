using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class UnionType : RpcType
    {
        public IReadOnlyCollection<RpcType> Types { get; }

        public UnionType(IReadOnlyCollection<RpcType> types)
        {
            Types = types;
        }

        public override string? Name { get; } = null;
    }
}