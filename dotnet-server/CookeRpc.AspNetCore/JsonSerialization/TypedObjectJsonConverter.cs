using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class TypedObjectJsonConverter<T> : JsonConverter<T>
    {
        private readonly ITypeBinder _typeBinder;

        public TypedObjectJsonConverter(ITypeBinder typeBinder)
        {
            _typeBinder = typeBinder;
        }

        // Goal is to replace this "temporary" functionality with more robust support for polymorphic deserialization
        // which seems to be on the road map for the .Net team
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var clrType = ResolveType(reader, typeToConvert);
            if (clrType != typeToConvert)
            {
                var value = JsonSerializer.Deserialize(ref reader, clrType, options);
                return value == null ? default : (T) value;
            }

            reader.Read();
            var obj = SerializerTools.ReadObjectProperties<T>(ref reader, options, clrType);

            if (obj is IJsonOnDeserialized deserialized) {
                deserialized.OnDeserialized();
            }
            
            return obj;
        }

        private Type ResolveType(Utf8JsonReader reader, Type typeToConvert)
        {
            reader.Read();
            
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                var propertyName = reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() : null;
                if (propertyName == "$type")
                {
                    reader.Read();
                    var typeName = reader.GetString() ?? throw new InvalidOperationException("Empty $type property value");
                    return _typeBinder.ResolveType(typeName, typeToConvert);
                }

                reader.Skip();
                if (!reader.Read())
                {
                    break;
                }
            }

            return typeToConvert;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var runtimeType = value.GetType();
            writer.WriteStartObject();
            writer.WritePropertyName("$type");
            writer.WriteStringValue(_typeBinder.GetName(runtimeType));

            SerializerTools.WriteObjectProperties(writer, value, options);

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
                   _typeBinder.ShouldResolveType(typeToConvert) &&
                   !typeToConvert.IsAssignableTo(typeof(IEnumerable)) && !typeToConvert.IsAssignableTo(typeof(string));
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter?) Activator.CreateInstance(
                typeof(TypedObjectJsonConverter<>).MakeGenericType(typeToConvert), _typeBinder);
        }
    }
}