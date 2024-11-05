namespace CookeRpc.AspNetCore.Model.Types;

public interface INamedRpcType : IRpcType
{
    public string Name { get; }
}
