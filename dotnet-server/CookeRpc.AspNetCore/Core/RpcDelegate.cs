using System.Threading.Tasks;

namespace CookeRpc.AspNetCore.Core
{
    public delegate Task<RpcResponse> RpcDelegate(RpcContext context);
}