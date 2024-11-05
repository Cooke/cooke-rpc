using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public class JsonRpcSerializer
    {
        private readonly JsonSerializerOptions _payloadSerializerOptions;
        private readonly JsonSerializerOptions _protocolSerializerOptions =
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public JsonRpcSerializer(JsonSerializerOptions payloadSerializerOptions)
        {
            _payloadSerializerOptions = payloadSerializerOptions;
        }

        public RpcInvocation Parse(ReadOnlySequence<byte> buffer)
        {
            var reader = new Utf8JsonReader(buffer);

            // Request array start
            reader.Read();

            // Header start
            reader.Read();
            var header =
                JsonSerializer.Deserialize<JsonRpcInvocationHeader>(
                    ref reader,
                    _protocolSerializerOptions
                ) ?? throw new JsonException("Failed to parse RPC invocation header");

            buffer = buffer.Slice(reader.BytesConsumed);
            var argumentState = reader.CurrentState;
            Func<Type, ValueTask<Optional<object?>>> consumeArgument = type =>
            {
                var argumentReader = new Utf8JsonReader(buffer, true, argumentState);

                // Next argument
                argumentReader.Read();
                var argumentStart = argumentReader.TokenStartIndex;
                if (argumentReader.TokenType == JsonTokenType.EndArray)
                {
                    return ValueTask.FromResult(new Optional<object?>());
                }

                argumentReader.Skip();

                var argument = DeserializeArgument(
                    buffer.Slice(argumentStart, argumentReader.BytesConsumed - argumentStart),
                    type
                );

                argumentState = argumentReader.CurrentState;
                buffer = buffer.Slice(argumentReader.BytesConsumed);

                return ValueTask.FromResult(new Optional<object?>(argument));
            };

            return new RpcInvocation(header.Id, header.Service, header.Proc, consumeArgument);
        }

        private object? DeserializeArgument(ReadOnlySequence<byte> argument, Type type)
        {
            var reader = new Utf8JsonReader(argument);
            return JsonSerializer.Deserialize(ref reader, type, _payloadSerializerOptions);
        }

        public void Serialize(RpcResponse response, IBufferWriter<byte> bufferWriter)
        {
            using var writer = new Utf8JsonWriter(bufferWriter);
            writer.WriteStartArray();

            switch (response)
            {
                case RpcError rpcError:
                    JsonSerializer.Serialize(
                        writer,
                        new JsonRpcReturnErrorHeader(response.Id, rpcError.Code, rpcError.Message),
                        _protocolSerializerOptions
                    );
                    break;

                case RpcReturnValue rpcReturnValue:
                    JsonSerializer.Serialize(
                        writer,
                        new JsonRpcReturnHeader { Id = response.Id },
                        _protocolSerializerOptions
                    );

                    if (rpcReturnValue.Value.HasValue)
                    {
                        writer.Flush();
                        bufferWriter.Write(new[] { (byte)',' });
                        SerializeReturnValue(
                            bufferWriter,
                            rpcReturnValue.Value.Value,
                            rpcReturnValue.DeclaredType
                        );
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(response));
            }

            writer.WriteEndArray();
            writer.Flush();
        }

        private void SerializeReturnValue(
            IBufferWriter<byte> bufferWriter,
            object? valueValue,
            Type type
        )
        {
            var utf8JsonWriter = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(utf8JsonWriter, valueValue, type, _payloadSerializerOptions);
            utf8JsonWriter.Flush();
        }

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

        public class JsonRpcInvocationHeader
        {
            [JsonPropertyName("id")]
            public string Id { get; init; } = "";

            [JsonPropertyName("proc")]
            public string Proc { get; init; } = "";

            [JsonPropertyName("service")]
            public string Service { get; init; } = "";
        }

        public class JsonRpcReturnHeader
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";
        }

        public class JsonRpcReturnErrorHeader
        {
            public JsonRpcReturnErrorHeader(string id, string errorCode, string errorMessage)
            {
                Id = id;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }

            [JsonPropertyName("id")]
            public string Id { get; }

            public string ErrorCode { get; }

            public string ErrorMessage { get; }
        }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
    }
}
