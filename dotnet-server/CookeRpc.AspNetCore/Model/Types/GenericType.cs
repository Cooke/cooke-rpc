using System.Collections.Generic;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class GenericType : RpcType
    {
        public RpcType InnerType { get; }
        
        public IReadOnlyCollection<RpcType> TypeArguments { get; }

        public GenericType(RpcType innerType, IReadOnlyCollection<RpcType> typeArguments)
        {
            InnerType = innerType;
            TypeArguments = typeArguments;
        }

        public override string? Name => InnerType.Name;
    }
}