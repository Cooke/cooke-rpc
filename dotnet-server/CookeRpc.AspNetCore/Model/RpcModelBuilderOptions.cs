using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelBuilderOptions
    {
        public static readonly ImmutableDictionary<Type, PrimitiveRpcType> DefaultPrimitiveTypeMap =
            new Dictionary<Type, PrimitiveRpcType>
            {
                { typeof(string), PrimitiveTypes.String },
                { typeof(int), PrimitiveTypes.Number },
                { typeof(long), PrimitiveTypes.Number },
                { typeof(ulong), PrimitiveTypes.Number },
                { typeof(uint), PrimitiveTypes.Number },
                { typeof(short), PrimitiveTypes.Number },
                { typeof(ushort), PrimitiveTypes.Number },
                { typeof(float), PrimitiveTypes.Number },
                { typeof(double), PrimitiveTypes.Number },
                { typeof(bool), PrimitiveTypes.Boolean },
                { typeof(TimeSpan), PrimitiveTypes.String },
                { typeof(DateTime), PrimitiveTypes.String },
                { typeof(DateTimeOffset), PrimitiveTypes.String },
                { typeof(decimal), PrimitiveTypes.Number },
                { typeof(void), PrimitiveTypes.Void },
                { typeof(object), PrimitiveTypes.Unknown },
                { typeof(IEnumerable<>), PrimitiveTypes.Array },
                { typeof(Optional<>), PrimitiveTypes.Optional },
                { typeof(ITuple), PrimitiveTypes.Tuple },
                { typeof(Dictionary<,>), PrimitiveTypes.Map },
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

        public IReadOnlyDictionary<Type, PrimitiveRpcType> PrimitiveTypeMap { get; init; } = DefaultPrimitiveTypeMap;

        public Type ContextType { get; init; } = typeof(RpcContext);

        public Func<ParameterInfo, ParameterResolver?>? CustomParameterResolver { get; init; } = null;

        public Func<MemberInfo, string> ProcedureNameFormatter { get; init; } = x => x.Name;

        public Action<Type, RpcModelBuilder>? OnAddingType { get; init; } = null;
    }
}