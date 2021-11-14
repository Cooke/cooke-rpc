using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model.Types
{
    public class NativeTypes
    {
        public static RefType Null { get; } = new(new RpcNativeTypeDefinition("null", typeof(object)));
        public static RefType String { get; } = new(new RpcNativeTypeDefinition("string", typeof(string)));
        public static RefType Number { get; } = new(new RpcNativeTypeDefinition("number", typeof(double)));
        public static RefType Boolean { get; } = new(new RpcNativeTypeDefinition("boolean", typeof(bool)));
        public static RefType Void { get; } = new(new RpcNativeTypeDefinition("void", typeof(void)));
        public static RefType Map { get; } = new(new RpcNativeTypeDefinition("map", typeof(Dictionary<,>)));
        public static RefType Array { get; } = new(new RpcNativeTypeDefinition("array", typeof(List<>)));
        public static RefType Tuple { get; } = new(new RpcNativeTypeDefinition("tuple", typeof(ITuple)));
        public static RefType Optional { get; } = new(new RpcNativeTypeDefinition("optional", typeof(Optional<>)));
        public static RefType Any { get; } = new(new RpcNativeTypeDefinition("any", typeof(object)));
    }
}