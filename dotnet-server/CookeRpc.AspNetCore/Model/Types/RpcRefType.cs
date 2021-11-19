using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RpcRefType : RpcType
    {
        public RpcTypeDefinition TypeDefinition { get; }

        public RpcRefType(RpcTypeDefinition typeDefinition)
        {
            TypeDefinition = typeDefinition;
        }

        public override string Name => TypeDefinition.Name;
    }
}