using System;

namespace CookeRpc.AspNetCore.Core
{
    public interface ITypeBinder
    {
        string GetName(Type type);

        Type ResolveType(string typeName, Type targetType);
    }
}