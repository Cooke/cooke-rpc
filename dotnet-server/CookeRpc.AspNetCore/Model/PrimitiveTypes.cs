using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class PrimitiveTypes
    {
        public static PrimitiveRpcType Null { get; } = new PrimitiveRpcType("null", typeof(object));
        public static PrimitiveRpcType Unknown { get; } = new PrimitiveRpcType("unknown", typeof(object));
        public static PrimitiveRpcType String { get; } = new PrimitiveRpcType("string", typeof(string));
        public static PrimitiveRpcType Number { get; } = new PrimitiveRpcType("number", typeof(object));
        public static PrimitiveRpcType Boolean { get; } = new PrimitiveRpcType("boolean", typeof(bool));
        public static PrimitiveRpcType Void { get; } = new PrimitiveRpcType("void", typeof(void));
        public static PrimitiveRpcType Map { get; } = new PrimitiveRpcType("map", typeof(Dictionary<,>));
        public static PrimitiveRpcType Array { get; } = new PrimitiveRpcType("array", typeof(List<>));
        public static PrimitiveRpcType Tuple { get; } = new PrimitiveRpcType("tuple", typeof(ITuple));
        public static PrimitiveRpcType Optional { get; } = new PrimitiveRpcType("optional", typeof(Optional<>));

        public static IReadOnlyCollection<PrimitiveRpcType> All { get; } = ReflectionHelper
            .GetAllStaticProperties<PrimitiveRpcType>(typeof(PrimitiveTypes)).ToArray();
    }
}