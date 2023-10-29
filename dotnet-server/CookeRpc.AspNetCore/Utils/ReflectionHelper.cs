using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CookeRpc.AspNetCore.Utils
{
    public static class ReflectionHelper
    {
        public static Type[] FindAllConcreteTypesOfType(Type type)
        {
            var allTypes = GetAllUserTypes();
            return allTypes.Where(x => type.IsAssignableFrom(x) && !x.IsAbstract).ToArray();
        }

        public static Type[] FindTypesWithAttribute<TAttribute>()
        {
            return FindTypesWithAttribute(typeof(TAttribute));
        }

        public static Type[] FindTypesWithAttribute(Type attributeType)
        {
            var allTypes = GetAllUserTypes();
            return allTypes.Where(x => x.GetCustomAttribute(attributeType) != null).ToArray();
        }

        public static IReadOnlyCollection<Type> FindAllOfType(Type type)
        {
            return GetAllUserTypes().Where(type.IsAssignableFrom).ToArray();
        }

        public static IEnumerable<Type> GetAllUserTypes()
        {
            var ignoredAssembliesNamespaces = new[] {"Microsoft", "System"};
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !ignoredAssembliesNamespaces.Any(y => x.FullName!.StartsWith(y)))
                .SelectMany(x => x.GetTypes());
        }

        public static Type? GetGenericTypeOfDefinition(Type type, Type definition)
        {
            return type.GetInterfaces().Concat(new[] {type}).FirstOrDefault(x =>
                x.IsConstructedGenericType && x.GetGenericTypeDefinition() == definition);
        }

        public static IEnumerable<TProperty> GetAllStaticProperties<TProperty>(Type t)
        {
            var propertyInfos = t.GetProperties(BindingFlags.Static | BindingFlags.Public);
            var fieldInfos = t.GetFields(BindingFlags.Static | BindingFlags.Public);
            return propertyInfos.Select(x => x.GetValue(null)).OfType<TProperty>()
                .Concat(fieldInfos.Select(x => x.GetValue(null)).OfType<TProperty>());
        }

        public static IEnumerable<MemberInfo> GetAllStaticProperties(Type t)
        {
            var propertyInfos = t.GetProperties(BindingFlags.Static | BindingFlags.Public);
            var fieldInfos = t.GetFields(BindingFlags.Static | BindingFlags.Public);
            return propertyInfos.Cast<MemberInfo>().AsEnumerable().Concat(fieldInfos.AsEnumerable());
        }

        public static bool IsNullable(MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo propertyInfo => IsNullable(propertyInfo),
                FieldInfo fieldInfo => IsNullable(fieldInfo),
                _ => throw new NotSupportedException()
            };
        }

        public static bool IsNullableReturn(MethodInfo methodInfo)
        {
            if (IsNullable(methodInfo.ReturnParameter))
            {
                return true;
            }

            if (GetGenericTypeOfDefinition(methodInfo.ReturnType, typeof(Task<>)) != null)
            {
                var nullable = methodInfo.ReturnParameter.CustomAttributes.FirstOrDefault(x =>
                    x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
                if (nullable != null && nullable.ConstructorArguments.Count == 1)
                {
                    var attributeArgument = nullable.ConstructorArguments[0];
                    if (attributeArgument.ArgumentType == typeof(byte[]))
                    {
                        var args = (ReadOnlyCollection<CustomAttributeTypedArgument>) attributeArgument.Value!;
                        if (args.Count > 0 && args[1].ArgumentType == typeof(byte))
                        {
                            return (byte) args[1].Value! == 2;
                        }
                    }
                    else if (attributeArgument.ArgumentType == typeof(byte))
                    {
                        return (byte) attributeArgument.Value! == 2;
                    }
                }
            }

            return false;
        }

        public static bool IsNullable(PropertyInfo property) =>
            IsNullableHelper(property.PropertyType, property.DeclaringType, property.CustomAttributes);

        public static bool IsNullable(FieldInfo field) =>
            IsNullableHelper(field.FieldType, field.DeclaringType, field.CustomAttributes);

        public static bool IsNullable(ParameterInfo parameter) =>
            IsNullableHelper(parameter.ParameterType, parameter.Member, parameter.CustomAttributes);

        private static bool IsNullableHelper(Type type,
            MemberInfo? member,
            IEnumerable<CustomAttributeData> customAttributes)
        {
            if (type.IsValueType)
                return Nullable.GetUnderlyingType(type) != null;

            var nullable = customAttributes.FirstOrDefault(x =>
                x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
            if (nullable != null && nullable.ConstructorArguments.Count == 1)
            {
                var attributeArgument = nullable.ConstructorArguments[0];
                if (attributeArgument.ArgumentType == typeof(byte[]))
                {
                    var args = (ReadOnlyCollection<CustomAttributeTypedArgument>) attributeArgument.Value!;
                    if (args.Count > 0 && args[0].ArgumentType == typeof(byte))
                    {
                        return (byte) args[0].Value! == 2;
                    }
                }
                else if (attributeArgument.ArgumentType == typeof(byte))
                {
                    return (byte) attributeArgument.Value! == 2;
                }
            }

            for (var declaringType = member; declaringType != null; declaringType = declaringType.DeclaringType)
            {
                var context = declaringType.CustomAttributes.FirstOrDefault(x =>
                    x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
                if (context != null && context.ConstructorArguments.Count == 1 &&
                    context.ConstructorArguments[0].ArgumentType == typeof(byte))
                {
                    return (byte) context.ConstructorArguments[0].Value! == 2;
                }
            }

            // Couldn't find a suitable attribute
            return false;
        }

        public static Type? GetTaskType(this Type taskType)
        {
            var genericTaskType = GetGenericTypeOfDefinition(taskType, typeof(Task<>));
            if (genericTaskType != null)
            {
                return genericTaskType.GetGenericArguments()[0];
            }
            
            genericTaskType = GetGenericTypeOfDefinition(taskType, typeof(ValueTask<>));
            if (genericTaskType != null)
            {
                return genericTaskType.GetGenericArguments()[0];
            }

            return typeof(Task).IsAssignableFrom(taskType) ? typeof(void) : null;
        }
    }
}