using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcParameterModel
    {
        public RpcParameterModel(string name, RpcType type, bool isOptional)
        {
            Name = name;
            Type = type;
            IsOptional = isOptional;
        }

        public string Name { get; init; }

        public RpcType Type { get; init; }

        public bool IsOptional { get; init; }
    }
}