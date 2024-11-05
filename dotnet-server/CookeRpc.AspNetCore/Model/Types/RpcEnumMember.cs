﻿namespace CookeRpc.AspNetCore.Model.Types;

public class RpcEnumMember
{
    public RpcEnumMember(string name, int value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}
