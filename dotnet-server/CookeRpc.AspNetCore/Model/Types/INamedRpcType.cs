namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public interface INamedRpcType : IRpcType
    {
        public string Name { get; }
    }
}