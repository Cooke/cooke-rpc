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
            {typeof(void), NativeTypes.Void},
            {typeof(string), NativeTypes.String},
            {typeof(int), NativeTypes.Number},
            {typeof(long), NativeTypes.Number},
            {typeof(ulong), NativeTypes.Number},
            {typeof(uint), NativeTypes.Number},
            {typeof(short), NativeTypes.Number},
            {typeof(ushort), NativeTypes.Number},
            {typeof(float), NativeTypes.Number},
            {typeof(double), NativeTypes.Number},
            {typeof(bool), NativeTypes.Boolean},
            {typeof(TimeSpan), NativeTypes.String},
            {typeof(DateTime), NativeTypes.String},
            {typeof(DateTimeOffset), NativeTypes.String},
            {typeof(decimal), NativeTypes.Number},
        }.ToImmutableDictionary();

        public Func<Type, bool> InterfaceFilter { get; init; } =
            t => t.Namespace != null && !t.Namespace.StartsWith("System");

        public Func<Type, bool> TypeFilter { get; init; } = t => !IsReflectionType(t);

        private static bool IsReflectionType(Type type) =>
            type == typeof(Type) || type.Namespace?.StartsWith("System.Reflection") == true;

        public Func<MemberInfo, string> MemberNameFormatter { get; init; } = memberInfo =>
            Char.ToLower(memberInfo.Name[0]) + memberInfo.Name.Substring(1);

        public Func<Type, string> TypeNameFormatter { get; init; } = type =>
            type.GetCustomAttribute<RpcTypeAttribute>()?.Name ??
            (type.IsInterface && type.Name.StartsWith("I") ? type.Name.Substring(1) : type.Name);

        public BindingFlags MemberBindingFilter { get; init; } = BindingFlags.Public | BindingFlags.Instance;

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

        public Func<Type, RpcModel, RpcTypeDefinition?> CustomTypeDefiner { get; init; } = (_, _) => null;

        public Func<Type, RpcType?> CustomTypeResolver { get; init; } = _ => null;

        public Func<MemberInfo, string> ProcedureNameFormatter { get; init; } = x => x.Name;
    }
}