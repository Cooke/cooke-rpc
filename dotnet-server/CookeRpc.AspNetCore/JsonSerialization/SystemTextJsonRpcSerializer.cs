using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookeRpc.AspNetCore.Core;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class SystemTextJsonRpcSerializer : JsonRpcSerializerBase
    {
        private readonly JsonSerializerOptions _payloadSerializerOptions;

        public SystemTextJsonRpcSerializer(ITypeBinder typeBinder, Action<JsonSerializerOptions>? configure = null)
        {
            _payloadSerializerOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new OptionalRpcJsonConverterFactory(),
                    new JsonStringEnumConverter(),
                },
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                
            };
            
            configure?.Invoke(_payloadSerializerOptions);
        }

        protected override object? DeserializeArgument(ReadOnlySequence<byte> argument, Type type)
        {
            var reader = new Utf8JsonReader(argument);
            return JsonSerializer.Deserialize(ref reader, type, _payloadSerializerOptions);
        }

        protected override void SerializeReturnValue(IBufferWriter<byte> bufferWriter, object? valueValue, Type type)
        {
            var utf8JsonWriter = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(utf8JsonWriter, valueValue, type, _payloadSerializerOptions);
            utf8JsonWriter.Flush();
        }
    }
}