using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class NestedTypedObjectJsonConverter<T> : JsonConverter<T>
    {
        private readonly ITypeBinder _typeBinder;

        public NestedTypedObjectJsonConverter(ITypeBinder typeBinder)
        {
            _typeBinder = typeBinder;
        }

        // Goal is to replace this "temporary" functionality with more robust support for polymorphic deserialization
        // which seems to be on the road map for the .Net team
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Read();

            var potentialTypeString = reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() : null;
            var clrType = typeToConvert;
            if (potentialTypeString?.StartsWith("$") == true)
            {
                clrType = _typeBinder.ResolveType(potentialTypeString.Substring(1), typeToConvert);
                reader.Read(); // property value (null or object start)
                if (reader.TokenType == JsonTokenType.Null)
                {
                    reader.Read();
                    GuardToken(JsonTokenType.EndObject, reader.TokenType);

                    // Move to next token and return null
                    reader.Read();
                    return default;
                }

                GuardToken(JsonTokenType.StartObject, reader.TokenType);
                reader.Read(); // Move into object
            }

            var value = SerializerTools.ReadObjectProperties<T>(ref reader, options, clrType);

            reader.Read();
            return value;
        }

        private static void GuardToken(JsonTokenType expectedToken, JsonTokenType actualToken)
        {
            if (actualToken != expectedToken)
            {
                throw new JsonException($"Parsed token {actualToken} but expected {expectedToken}");
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(
                "$" + _typeBinder.GetName(value.GetType()) ?? throw new InvalidOperationException());
            writer.WriteStartObject();

            SerializerTools.WriteObjectProperties(writer, value, options);

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    public class NestedTypedObjectConverterFactory : JsonConverterFactory
    {
        private readonly ITypeBinder _typeBinder;

        public NestedTypedObjectConverterFactory(ITypeBinder typeBinder)
        {
            _typeBinder = typeBinder;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return (typeToConvert.IsClass || typeToConvert.IsInterface) && !typeToConvert.IsAssignableTo(typeof(IEnumerable)) &&
                   !typeToConvert.IsAssignableTo(typeof(string)) && _typeBinder.ShouldResolveType(typeToConvert);
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter?) Activator.CreateInstance(
                typeof(NestedTypedObjectJsonConverter<>).MakeGenericType(typeToConvert),
                _typeBinder);
        }
    }
}