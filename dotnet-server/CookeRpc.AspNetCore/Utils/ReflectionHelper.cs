using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CookeRpc.AspNetCore.Utils;

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
        var ignoredAssembliesNamespaces = new[] { "Microsoft", "System" };
        return AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(x => !ignoredAssembliesNamespaces.Any(y => x.FullName!.StartsWith(y)))
            .SelectMany(x => x.GetTypes());
    }

    public static Type? GetGenericTypeOfDefinition(Type type, Type definition)
    {
        return type.GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(x =>
                x.IsConstructedGenericType && x.GetGenericTypeDefinition() == definition
            );
    }

    public static IEnumerable<TProperty> GetAllStaticProperties<TProperty>(Type t)
    {
        var propertyInfos = t.GetProperties(BindingFlags.Static | BindingFlags.Public);
        var fieldInfos = t.GetFields(BindingFlags.Static | BindingFlags.Public);
        return propertyInfos
            .Select(x => x.GetValue(null))
            .OfType<TProperty>()
            .Concat(fieldInfos.Select(x => x.GetValue(null)).OfType<TProperty>());
    }

    public static IEnumerable<MemberInfo> GetAllStaticProperties(Type t)
    {
        var propertyInfos = t.GetProperties(BindingFlags.Static | BindingFlags.Public);
        var fieldInfos = t.GetFields(BindingFlags.Static | BindingFlags.Public);
        return propertyInfos.Cast<MemberInfo>().AsEnumerable().Concat(fieldInfos.AsEnumerable());
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
