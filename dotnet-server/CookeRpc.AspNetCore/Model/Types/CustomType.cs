using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class CustomType : RpcType
    {
        public RpcTypeDefinition TypeDefinition { get; }

        public CustomType(RpcTypeDefinition typeDefinition)
        {
            TypeDefinition = typeDefinition;
        }

        public override string Name => TypeDefinition.Name;
    }
}