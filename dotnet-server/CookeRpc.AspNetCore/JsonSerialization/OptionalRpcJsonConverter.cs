using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class OptionalRpcJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override Optional<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            return new Optional<T>(value!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Optional<T> value,
            JsonSerializerOptions options
        )
        {
            throw new NotSupportedException();
        }
    }

    public class OptionalRpcJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

        public override JsonConverter? CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            return (JsonConverter?)
                Activator.CreateInstance(
                    typeof(OptionalRpcJsonConverter<>).MakeGenericType(
                        typeToConvert.GetGenericArguments().Single()
                    )
                );
        }
    }
}
