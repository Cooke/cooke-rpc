using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public static class SerializerTools
    {
        public static T ReadObjectProperties<T>(ref Utf8JsonReader reader, JsonSerializerOptions options, Type clrType)
        {
            var ctor = clrType.GetConstructors().FirstOrDefault();
            if (ctor == null)
            {
                throw new Exception($"Cannot create an instance of {clrType}");
            }
            
            var ctorParameters = ctor.GetParameters()
                .ToDictionary(x => x.Name ?? throw new NotSupportedException("Unnamed parameters are not supported"),
                    StringComparer.OrdinalIgnoreCase);
            var props = clrType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var fields = clrType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            while (reader.TokenType != JsonTokenType.EndObject)
            {
                var propertyName = reader.GetString() ?? throw new JsonException("Invalid property name");
                reader.Read();
                if (props.TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite)
                {
                    map.Add(propertyName, JsonSerializer.Deserialize(ref reader, propertyInfo.PropertyType, options));
                }
                else if (fields.TryGetValue(propertyName, out var fieldInfo))
                {
                    map.Add(propertyName, JsonSerializer.Deserialize(ref reader, fieldInfo.FieldType, options));
                }
                else if (ctorParameters.TryGetValue(propertyName, out var ctorParameterInfo))
                {
                    map.Add(propertyName, JsonSerializer.Deserialize(ref reader, ctorParameterInfo.ParameterType, options));
                }
                else {
                    reader.Skip();
                }

                reader.Read();
            }

            var ctorArgs = ctorParameters.Select(p => map.GetValueOrDefault(p.Key)).ToArray();
            var obj = (T) (Activator.CreateInstance(clrType, ctorArgs) ?? throw new InvalidOperationException());

            foreach (var prop in props)
            {
                if (map.TryGetValue(prop.Key, out var propValue) && prop.Value.CanWrite)
                {
                    prop.Value.SetValue(obj, propValue);
                }
            }

            foreach (var field in fields)
            {
                if (map.TryGetValue(field.Key, out var propValue))
                {
                    field.Value.SetValue(obj, propValue);
                }
            }

            return obj;
        }

        public static void WriteObjectProperties<T>(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            foreach (var propertyInfo in value!.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                WriteProperty(propertyInfo.Name, propertyInfo.GetValue(value), propertyInfo.PropertyType);
            }

            foreach (var fieldInfo in value!.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                WriteProperty(fieldInfo.Name, fieldInfo.GetValue(value), fieldInfo.FieldType);
            }

            void WriteProperty(string propName, object? propValue, Type propType)
            {
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(propName) ?? propName);
                JsonSerializer.Serialize(writer, propValue, propType, options);
            }
        }
    }
}