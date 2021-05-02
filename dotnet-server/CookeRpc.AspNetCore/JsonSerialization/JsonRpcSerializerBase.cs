using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.JsonSerialization
{
    public abstract class JsonRpcSerializerBase : IRpcSerializer
    {
        private readonly JsonSerializerOptions _protocolSerializerOptions;

        protected JsonRpcSerializerBase()
        {
            _protocolSerializerOptions = new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase};
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
                var argumentStart = argumentReader.TokenStartIndex;
                if (argumentReader.TokenType == JsonTokenType.EndArray)
                {
                    return ValueTask.FromResult(new Optional<object?>());
                }

                argumentReader.Skip();

                var argument = DeserializeArgument(
                    buffer.Slice(argumentStart,
                        argumentReader.BytesConsumed - argumentStart), 
                    type);
                // var deserialize = JsonSerializer.Deserialize(ref argumentReader, type, _payloadSerializerOptions);

                argumentState = argumentReader.CurrentState;
                buffer = buffer.Slice(argumentReader.BytesConsumed);

                return ValueTask.FromResult(new Optional<object?>(argument));
            };

            return new RpcInvocation(header.Id, header.Service, header.Proc, consumeArgument);
        }

        protected abstract object? DeserializeArgument(ReadOnlySequence<byte> argument, Type type);

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
                        writer.Flush();
                        bufferWriter.Write(new[] {(byte) ','});
                        SerializeReturnValue(bufferWriter, rpcReturnValue.Value.Value);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(response));
            }

            writer.WriteEndArray();
            writer.Flush();
        }

        protected abstract void SerializeReturnValue(IBufferWriter<byte> bufferWriter, object? valueValue);

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