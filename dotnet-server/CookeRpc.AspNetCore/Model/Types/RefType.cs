using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class RefType : RpcType
    {
        public RpcTypeDefinition TypeDefinition { get; }

        public RefType(RpcTypeDefinition typeDefinition)
        {
            TypeDefinition = typeDefinition;
        }

        public override string Name => TypeDefinition.Name;
    }
}