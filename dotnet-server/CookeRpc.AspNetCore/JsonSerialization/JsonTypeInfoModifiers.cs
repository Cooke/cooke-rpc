using System.Linq;
using System.Text.Json.Serialization.Metadata;
using CookeRpc.AspNetCore.Model;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.JsonSerialization;

public class JsonTypeInfoModifiers
{
    public static void RpcPolymorphismJsonTypeInfoModifier(RpcModel model, JsonTypeInfo info)
    {
        if (info.PolymorphismOptions != null) {
            return;
        }
        
        var rpcType = model.Types.FirstOrDefault(x => x.ClrType == info.Type);
        if (rpcType == null) {
            return;
        }
        
        var extenders = model.Types.OfType<ObjectRpcType>().Where(x => x.Extends.Any(x => x == rpcType)).ToArray();
        if (extenders.Length <= 0) {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions();
        foreach (var extender in extenders) {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(extender.ClrType, extender.Name));
        }
    }
}