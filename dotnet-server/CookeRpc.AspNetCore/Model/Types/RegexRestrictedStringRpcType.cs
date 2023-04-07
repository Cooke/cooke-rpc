using System;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model.Types
{
    public record RegexRestrictedStringRpcType(string Pattern) : IRpcType
    {
        public Type ClrType => typeof(string);
    }
}