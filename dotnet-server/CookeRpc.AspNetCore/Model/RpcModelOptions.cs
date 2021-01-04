using System;
using System.Reflection;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelOptions
    {
        public Func<Type, bool> TypeFilter { get; init; } = type => true;

        public Func<MemberInfo, string> MemberNameFormatter { get; init; } = memberInfo =>
            Char.ToLower(memberInfo.Name[0]) + memberInfo.Name.Substring(1);

        public Func<Type, string> TypeNameFormatter { get; init; } = type =>
            type.GetCustomAttribute<RpcType>()?.Name ??
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
    }
}