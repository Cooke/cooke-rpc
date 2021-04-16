using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Serialization;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelOptions
    {
        public static readonly ImmutableDictionary<Type, RpcType> DefaultTypeMap = new Dictionary<Type, RpcType>
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
            {typeof(decimal), NativeType.Number},
            {typeof(object), NativeType.Any}
        }.ToImmutableDictionary();

        public Func<Type, bool> TypeFilter { get; init; } = t => !IsReflectionType(t);

        private static bool IsReflectionType(Type type) =>
            type == typeof(Type) || type.Namespace?.StartsWith("System.Reflection") == true;

        public Func<MemberInfo, string> MemberNameFormatter { get; init; } = memberInfo =>
            Char.ToLower(memberInfo.Name[0]) + memberInfo.Name.Substring(1);

        public Func<Type, string> TypeNameFormatter { get; init; } = type =>
            type.GetCustomAttribute<RpcTypeAttribute>()?.Name ??
            (type.IsInterface && type.Name.StartsWith("I") ? type.Name.Substring(1) : type.Name);

        public BindingFlags MemberBindingFilter { get; init; } =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        public Func<MemberInfo, bool> MemberFilter { get; init; } = info =>
        {
            return info switch
            {
                _ when info.GetCustomAttribute<IgnoreDataMemberAttribute>() != null => false,
                FieldInfo fi => !IsReflectionType(fi.FieldType),
                PropertyInfo pi => !IsReflectionType(pi.PropertyType),
                _ => false
            };
        };

        public Func<MemberInfo, bool> IsMemberOptional { get; init; } = ReflectionHelper.IsNullable;

        public Func<MemberInfo, bool> IsMemberNullable { get; init; } = ReflectionHelper.IsNullable;

        public Func<string, string> EnumMemberNameFormatter { get; init; } = name => name;

        public Func<Type, string> ServiceNameFormatter { get; init; } = type => type.Name;

        public IReadOnlyDictionary<Type, RpcType> CustomDefaultTypeMap { get; init; } = DefaultTypeMap;

        public Type ContextType { get; init; } = typeof(RpcContext);

        public Func<ParameterInfo, ParameterResolver?>? CustomParameterResolver { get; init; } = null;

        public Func<Type, RpcTypeDefinition?> CustomTypeDefiner { get; init; } = _ => null;
        
        public Func<Type, RpcType?> CustomTypeResolver { get; init; } = _ => null;
    }
}