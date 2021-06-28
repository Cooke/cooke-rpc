namespace CookeRpc.AspNetCore.Model.Types
{
    public class NativeType : RpcType
    {
        public static NativeType Null { get; } = new("null");
        public static NativeType String { get; } = new("string");
        public static NativeType Number { get; } = new("number");
        public static NativeType Boolean { get; } = new("boolean");
        public static NativeType Void { get; } = new("void");
        public static NativeType Map { get; } = new("map");
        public static NativeType Array { get; } = new("array");
        public static NativeType Tuple { get; } = new("tuple");
        public static NativeType Optional { get; } = new("optional");
        public static NativeType Any { get; } = new("any");

        public NativeType(string name)
        {
            Name = name;
        }

        public override string Name { get; }
    }
}