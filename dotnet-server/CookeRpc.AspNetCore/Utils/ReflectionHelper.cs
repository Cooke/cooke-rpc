using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Namotion.Reflection;

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

        public static bool IsNullable(MemberInfo memberInfo)
        {
            return memberInfo.ToContextualMember().Nullability == Nullability.Nullable;
            //
            // var nullableAttribute =
            //     memberInfo.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "NullableAttribute");
            // if (nullableAttribute != null && nullableAttribute.ConstructorArguments.First().Value!.Equals((byte) 2))
            // {
            //     return true;
            // }
            //
            // return IsNullable(memberInfo switch
            // {
            //     PropertyInfo propertyInfo => propertyInfo.PropertyType,
            //     FieldInfo fieldInfo => fieldInfo.FieldType,
            //     _ => throw new NotSupportedException()
            // });
        }

        public static bool IsNullableReturn(MethodInfo methodInfo)
        {
            var returnInfo = methodInfo.ReturnParameter.ToContextualParameter();
            if (returnInfo.Nullability == Nullability.Nullable)
            {
                return true;
            }

            if (GetGenericTypeOfDefinition(methodInfo.ReturnType, typeof(Task<>)) != null)
            {
                return returnInfo.GenericArguments.First().Nullability == Nullability.Nullable;
            }

            return false;
        }

        private static bool IsNullable(Type type)
        {
            return GetGenericTypeOfDefinition(type, typeof(Nullable<>)) != null;
        }

        public static bool IsNullable(ParameterInfo parameter)
        {
            var nullableAttribute =
                parameter.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "NullableAttribute");
            if (nullableAttribute != null && nullableAttribute.ConstructorArguments.First().Value!.Equals((byte) 2))
            {
                return true;
            }

            return IsNullable(parameter.ParameterType);
        }

        public static Type? GetTaskType(this Type taskType)
        {
            var genericTaskType = GetGenericTypeOfDefinition(taskType, typeof(Task<>));
            if (genericTaskType != null)
            {
                return genericTaskType.GetGenericArguments()[0];
            }

            return typeof(Task).IsAssignableFrom(taskType) ? typeof(void) : null;
        }
    }
}