using System;
using System.Collections.Generic;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public record RegexRpcType(string Pattern) : IRpcType
    {
        public Type ClrType => typeof(string);
    }
}