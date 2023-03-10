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
        public static RpcPrimitiveType Null { get; } = new RpcPrimitiveType("null", typeof(object));
        public static RpcPrimitiveType Unknown { get; } = new RpcPrimitiveType("unknown", typeof(object));
        public static RpcPrimitiveType String { get; } = new RpcPrimitiveType("string", typeof(string));
        public static RpcPrimitiveType Number { get; } = new RpcPrimitiveType("number", typeof(double));
        public static RpcPrimitiveType Boolean { get; } = new RpcPrimitiveType("boolean", typeof(bool));
        public static RpcPrimitiveType Void { get; } = new RpcPrimitiveType("void", typeof(void));
        public static RpcPrimitiveType Map { get; } = new RpcPrimitiveType("map", typeof(Dictionary<,>));
        public static RpcPrimitiveType Array { get; } = new RpcPrimitiveType("array", typeof(List<>));
        public static RpcPrimitiveType Tuple { get; } = new RpcPrimitiveType("tuple", typeof(ITuple));

        public static RpcPrimitiveType Optional { get; } =
            new RpcPrimitiveType("optional", typeof(Optional<>));

        public static IReadOnlyCollection<RpcPrimitiveType> All { get; } = ReflectionHelper.GetAllStaticProperties<RpcPrimitiveType>(typeof(PrimitiveTypes)).ToArray();
    }
}