using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcProcedureModel
    {
        private readonly Lazy<RpcDelegate> _lazyDelegate;

        public RpcProcedureModel(string name,
            Lazy<RpcDelegate> @lazyDelegate,
            Types.RpcType returnType,
            IReadOnlyCollection<RpcParameterModel> parameters)
        {
            _lazyDelegate = lazyDelegate;
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
        }

        public string Name { get; }

        public RpcDelegate Delegate => _lazyDelegate.Value;

        public Types.RpcType ReturnType { get; }

        public IReadOnlyCollection<RpcParameterModel> Parameters { get; init; }
    }

    public class RpcServiceModel
    {
        public RpcServiceModel(Type rpcControllerType, string name, IReadOnlyCollection<RpcProcedureModel> procedures)
        {
            RpcControllerType = rpcControllerType;
            Name = name;
            Procedures = procedures;
        }

        public Type RpcControllerType { get; }
        public string Name { get; }
        
        public IReadOnlyCollection<RpcProcedureModel> Procedures { get; }
    }
}