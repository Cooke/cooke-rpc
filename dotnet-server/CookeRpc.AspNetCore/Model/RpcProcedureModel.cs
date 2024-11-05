using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model;

public class RpcProcedureModel
{
    private readonly Lazy<RpcDelegate> _lazyDelegate;

    public RpcProcedureModel(
        string name,
        Lazy<RpcDelegate> lazyDelegate,
        IRpcType returnType,
        IReadOnlyCollection<RpcParameterModel> parameters,
        IReadOnlyCollection<object> attributes
    )
    {
        _lazyDelegate = lazyDelegate;
        Name = name;
        ReturnType = returnType;
        Parameters = parameters;
        Attributes = attributes;
    }

    public string Name { get; }

    public RpcDelegate Delegate => _lazyDelegate.Value;

    public IRpcType ReturnType { get; }

    public IReadOnlyCollection<RpcParameterModel> Parameters { get; init; }

    public IReadOnlyCollection<object> Attributes { get; }
}

public class RpcServiceModel
{
    public RpcServiceModel(
        Type rpcControllerType,
        string name,
        IReadOnlyCollection<RpcProcedureModel> procedures,
        IReadOnlyCollection<object> attributes
    )
    {
        RpcControllerType = rpcControllerType;
        Name = name;
        Procedures = procedures;
        Attributes = attributes;
    }

    public Type RpcControllerType { get; }

    public string Name { get; }

    public IReadOnlyCollection<RpcProcedureModel> Procedures { get; }

    public IReadOnlyCollection<object> Attributes { get; }
}
