namespace CookeRpc.AspNetCore.Model
{
    public class RpcParameterModel
    {
        public RpcParameterModel(string name, Types.RpcType type, bool isOptional)
        {
            Name = name;
            Type = type;
            IsOptional = isOptional;
        }

        public string Name { get; init; }

        public Types.RpcType Type { get; init; }

        public bool IsOptional { get; init; }
    }
}