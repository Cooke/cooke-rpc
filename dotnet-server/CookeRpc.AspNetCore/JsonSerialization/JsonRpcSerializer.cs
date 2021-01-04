using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class JsonRpcSerializer : IRpcSerializer
    {
        private readonly JsonSerializerOptions _payloadSerializerOptions;
        private readonly JsonSerializerOptions _protocolSerializerOptions;

        public JsonRpcSerializer(ITypeBinder typeBinder)
        {
            _protocolSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _payloadSerializerOptions = new JsonSerializerOptions
            {
                Converters = {new TypedObjectConverterFactory(typeBinder), new JsonStringEnumConverter()},
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public RpcInvocation Parse(ReadOnlySequence<byte> buffer)
        {
            var reader = new Utf8JsonReader(buffer);

            // Request array start
            reader.Read();

            // Header start
            reader.Read();
            var header = JsonSerializer.Deserialize<JsonRpcInvocationHeader>(ref reader, _protocolSerializerOptions) ??
                         throw new JsonException("Failed to parse RPC invocation header");

            buffer = buffer.Slice(reader.BytesConsumed);
            var argumentState = reader.CurrentState;
            Func<Type, ValueTask<Optional<object?>>> consumeArgument = type =>
            {
                var argumentReader = new Utf8JsonReader(buffer, true, argumentState);

                // Next argument
                argumentReader.Read();
                if (argumentReader.TokenType == JsonTokenType.EndArray)
                {
                    return ValueTask.FromResult(new Optional<object?>());
                }

                var deserialize = JsonSerializer.Deserialize(ref argumentReader, type, _payloadSerializerOptions);

                argumentState = argumentReader.CurrentState;
                buffer = buffer.Slice(argumentReader.BytesConsumed);

                return ValueTask.FromResult(new Optional<object?>(deserialize));
            };

            return new RpcInvocation(header.Id, header.Service, header.Proc, consumeArgument);
        }

        public void Serialize(RpcResponse response, IBufferWriter<byte> bufferWriter)
        {
            using var writer = new Utf8JsonWriter(bufferWriter);
            writer.WriteStartArray();

            switch (response)
            {
                case RpcError rpcError:
                    JsonSerializer.Serialize(writer,
                        new JsonRpcReturnErrorHeader(response.Id, rpcError.Code, rpcError.Message),
                        _protocolSerializerOptions);
                    break;

                case RpcReturnValue rpcReturnValue:
                    JsonSerializer.Serialize(writer, new JsonRpcReturnHeader {Id = response.Id},
                        _protocolSerializerOptions);

                    if (rpcReturnValue.Value.HasValue)
                    {
                        JsonSerializer.Serialize(writer, rpcReturnValue.Value.Value,
                            rpcReturnValue.Value.Value?.GetType() ?? typeof(object), _payloadSerializerOptions);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(response));
            }

            writer.WriteEndArray();
            writer.Flush();
        }

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

        public class JsonRpcInvocationHeader
        {
            [JsonPropertyName("id")] public string Id { get; init; } = "";

            [JsonPropertyName("proc")] public string Proc { get; init; } = "";

            [JsonPropertyName("service")] public string Service { get; init; } = "";
        }

        public class JsonRpcReturnHeader
        {
            [JsonPropertyName("id")] public string Id { get; set; } = "";
        }

        public class JsonRpcReturnErrorHeader
        {
            public JsonRpcReturnErrorHeader(string id, string errorCode, string errorMessage)
            {
                Id = id;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }

            [JsonPropertyName("id")] public string Id { get; }


            public string ErrorCode { get; }

            public string ErrorMessage { get; }
        }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
    }
}