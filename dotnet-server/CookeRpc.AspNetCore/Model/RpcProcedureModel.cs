using System.Collections.Generic;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcProcedureModel
    {
        public RpcProcedureModel(string name,
            RpcDelegate @delegate,
            Types.RpcType returnType,
            IReadOnlyCollection<RpcParameterModel> parameters)
        {
            Name = name;
            Delegate = @delegate;
            ReturnType = returnType;
            Parameters = parameters;
        }

        public string Name { get; }

        public RpcDelegate Delegate { get; }

        public Types.RpcType ReturnType { get; }

        public IReadOnlyCollection<RpcParameterModel> Parameters { get; init; }
    }

    public class RpcServiceModel
    {
        public RpcServiceModel(string name, IReadOnlyCollection<RpcProcedureModel> procedures)
        {
            Name = name;
            Procedures = procedures;
        }

        public string Name { get; }
        
        public IReadOnlyCollection<RpcProcedureModel> Procedures { get; }
    }
}