using System;
using System.Buffers;

namespace CookeRpc.AspNetCore.Core
{
    public interface IRpcSerializer
    {
        RpcInvocation Parse(ReadOnlySequence<byte> buffer);
        
        void Serialize(RpcResponse response, IBufferWriter<byte> bufferWriter, Type returnType);
    }
}