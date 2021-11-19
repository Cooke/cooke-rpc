using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class PrimitiveTypes
    {
        public static RpcRefType Null { get; } = new(new RpcPrimitiveTypeDefinition("null", typeof(object)));
        public static RpcRefType Unknown { get; } = new(new RpcPrimitiveTypeDefinition("unknown", typeof(object)));
        public static RpcRefType String { get; } = new(new RpcPrimitiveTypeDefinition("string", typeof(string)));
        public static RpcRefType Number { get; } = new(new RpcPrimitiveTypeDefinition("number", typeof(double)));
        public static RpcRefType Boolean { get; } = new(new RpcPrimitiveTypeDefinition("boolean", typeof(bool)));
        public static RpcRefType Void { get; } = new(new RpcPrimitiveTypeDefinition("void", typeof(void)));
        public static RpcRefType Map { get; } = new(new RpcPrimitiveTypeDefinition("map", typeof(Dictionary<,>)));
        public static RpcRefType Array { get; } = new(new RpcPrimitiveTypeDefinition("array", typeof(List<>)));
        public static RpcRefType Tuple { get; } = new(new RpcPrimitiveTypeDefinition("tuple", typeof(ITuple)));

        public static RpcRefType Optional { get; } =
            new(new RpcPrimitiveTypeDefinition("optional", typeof(Optional<>)));
    }
}