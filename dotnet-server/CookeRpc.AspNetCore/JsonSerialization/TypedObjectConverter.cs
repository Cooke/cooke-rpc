using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class TypedObjectConverter<T> : JsonConverter<T>
    {
        private readonly ITypeBinder _typeBinder;

        public TypedObjectConverter(ITypeBinder typeBinder)
        {
            _typeBinder = typeBinder;
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Read();

            var potentialTypeString = reader.GetString() ?? throw new JsonException("Property name missing");
            var clrType = typeToConvert;
            if (potentialTypeString.StartsWith("$"))
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

            // TODO potentially improve performance by generating code instead of using reflection 
            var obj = (T) (Activator.CreateInstance(clrType) ?? throw new InvalidOperationException());
            var props = clrType.GetProperties().ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            while (reader.TokenType != JsonTokenType.EndObject)
            {
                var propertyName = reader.GetString() ?? throw new JsonException("Invalid property name");
                reader.Read();
                if (props.TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite)
                {
                    propertyInfo.SetValue(obj,
                        JsonSerializer.Deserialize(ref reader, propertyInfo.PropertyType, options));
                }

                reader.Read();
            }

            reader.Read();

            return obj;
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

            foreach (var propertyInfo in value!.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(propertyInfo.Name) ??
                                         propertyInfo.Name);
                JsonSerializer.Serialize(writer, propertyInfo.GetValue(value), options);
            }

            foreach (var fieldInfo in value!.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(fieldInfo.Name) ?? fieldInfo.Name);
                JsonSerializer.Serialize(writer, fieldInfo.GetValue(value), options);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    public class TypedObjectConverterFactory : JsonConverterFactory
    {
        private readonly ITypeBinder _typeBinder;

        public TypedObjectConverterFactory(ITypeBinder typeBinder)
        {
            _typeBinder = typeBinder;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return (typeToConvert.IsClass || typeToConvert.IsInterface) &&
                   !typeToConvert.IsAssignableTo(typeof(IEnumerable)) && !typeToConvert.IsAssignableTo(typeof(string));
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter?) Activator.CreateInstance(
                typeof(TypedObjectConverter<>).MakeGenericType(typeToConvert), _typeBinder);
        }
    }
}