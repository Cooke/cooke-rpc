using System;
using System.Collections.Generic;
using System.Reflection;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelOptions
    {
        public Func<Type, bool> TypeFilter { get; init; } = type => true;

        public Func<MemberInfo, string> MemberNameFormatter { get; init; } = memberInfo =>
            Char.ToLower(memberInfo.Name[0]) + memberInfo.Name.Substring(1);

        public Func<Type, string> TypeNameFormatter { get; init; } = type =>
            type.GetCustomAttribute<RpcTypeAttribute>()?.Name ??
            (type.IsInterface && type.Name.StartsWith("I") ? type.Name.Substring(1) : type.Name);

        public BindingFlags MemberBindingFilter { get; init; } =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        public Func<MemberInfo, bool> MemberFilter { get; init; } = info =>
        {
            switch (info)
            {
                case FieldInfo _:
                case PropertyInfo _:
                    return true;

                default:
                    return false;
            }
        };

        public Func<MemberInfo, bool> IsMemberOptional { get; init; } = ReflectionHelper.IsNullable;

        public Func<MemberInfo, bool> IsMemberNullable { get; init; } = ReflectionHelper.IsNullable;

        public Func<string, string> EnumMemberNameFormatter { get; init; } = name => name;

        public Func<Type, string> ServiceNameFormatter { get; init; } = type => type.Name;

        public IReadOnlyCollection<RpcTypeDefinition> InitialTypeDefinitions { get; init; } =
            ArraySegment<RpcTypeDefinition>.Empty;

        public IReadOnlyDictionary<Type, RpcType> InitialTypeMap { get; init; } = new Dictionary<Type, RpcType>
        {
            {typeof(void), NativeType.Void},
            {typeof(string), NativeType.String},
            {typeof(int), NativeType.Number},
            {typeof(long), NativeType.Number},
            {typeof(ulong), NativeType.Number},
            {typeof(uint), NativeType.Number},
            {typeof(short), NativeType.Number},
            {typeof(ushort), NativeType.Number},
            {typeof(float), NativeType.Number},
            {typeof(double), NativeType.Number},
            {typeof(bool), NativeType.Boolean},
            {typeof(TimeSpan), NativeType.String},
            {typeof(DateTime), NativeType.String},
            {typeof(DateTimeOffset), NativeType.String},
            {typeof(IDictionary<,>), NativeType.Map},
            {typeof(IEnumerable<>), NativeType.Array},
            {typeof(Optional<>), NativeType.Optional},
            {typeof(Decimal), NativeType.Number}
        };
    }
}