using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcParameterModel
    {
        public RpcParameterModel(string name, IRpcType type, bool isOptional)
        {
            Name = name;
            Type = type;
            IsOptional = isOptional;
        }

        public string Name { get; init; }

        public IRpcType Type { get; init; }

        public bool IsOptional { get; init; }
    }
}