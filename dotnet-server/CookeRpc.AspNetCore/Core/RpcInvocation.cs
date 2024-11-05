using System;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Core
{
    public record RpcInvocation
    {
        public RpcInvocation(
            string id,
            string service,
            string procedure,
            Func<Type, ValueTask<Optional<object?>>> consumeArgument
        )
        {
            Id = id;
            Service = service;
            Procedure = procedure;
            ConsumeArgument = consumeArgument;
        }

        public string Id { get; }

        public string Procedure { get; }

        public string Service { get; }

        public Func<Type, ValueTask<Optional<object?>>> ConsumeArgument { get; }
    }

    public abstract record RpcResponse(string Id);

    public record RpcReturnValue(string Id, Optional<object?> Value, Type DeclaredType)
        : RpcResponse(Id);

    public record RpcError : RpcResponse
    {
        public RpcError(string id, string code, string message, Exception? exception)
            : base(id)
        {
            Code = code;
            Message = message;
            Exception = exception;
        }

        public string Code { get; }

        public string Message { get; }

        public Exception? Exception { get; }
    }
}
