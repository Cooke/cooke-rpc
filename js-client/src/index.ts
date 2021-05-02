import { RpcError, RpcInvocation, RpcInvoker } from "./types";

export * from "./json";
export * from "./types";

export const rpcErrorCodes = {
  serverError: "server_error",
  networkError: "network_error",
  responseFormatError: "response_format_error",
};

type RpcJsonInvocationHeader = {
  id: string;
  service: string;
  proc: string;
};

type RpcJsonInvocation = [RpcJsonInvocationHeader, ...any[]];

type RpcJsonResponse<TReturn> =
  | RpcJsonErrorResponse
  | RpcJsonSuccessResponse<TReturn>;

type RpcJsonErrorResponse = [
  { id: string; errorCode: string; errorMessage?: string },
  any
];

type RpcJsonSuccessResponse<TReturn> = [{ id: string }, TReturn];

export async function sendJsonRpc<TResult>(
  invocation: RpcInvocation<TResult>,
  request: (json: string) => Promise<string>
) {
  const id = Math.random().toString();
  const transportInvocation: RpcJsonInvocation = [
    { id, service: invocation.service, proc: invocation.proc },
    ...invocation.args,
  ];
  const invocationJson = JSON.stringify(transportInvocation);

  let responseJson;
  try {
    responseJson = await request(invocationJson);
  } catch (error) {
    if (error instanceof RpcError) {
      throw error;
    }

    throw new RpcError(rpcErrorCodes.networkError, error?.message);
  }

  let rpcResponse: any;
  try {
    rpcResponse = JSON.parse(responseJson);
  } catch (error) {
    throw new RpcError(rpcErrorCodes.responseFormatError);
  }

  if (!isRpcResponse(rpcResponse)) {
    throw new RpcError(rpcErrorCodes.responseFormatError);
  }

  const rpcHeader = rpcResponse[0];
  if ("errorCode" in rpcHeader) {
    throw new RpcError(rpcHeader.errorCode, rpcHeader.errorMessage);
  }

  return rpcResponse[1];
}

function isRpcResponse(response: any): response is RpcJsonResponse<any> {
  if (!Array.isArray(response)) {
    return false;
  }

  if (response.length < 1) {
    return false;
  }

  const header = response[0];
  if (typeof header !== "object") {
    return false;
  }

  return true;
}

export function createRpcInvoker<
  TService extends string,
  TProc extends string,
  TArgs extends any[],
  TReturn
>(service: TService, proc: TProc): RpcInvoker<TArgs, TReturn> {
  const func = function (...args: TArgs): RpcInvocation<TReturn> {
    return { service, proc, args: args };
  };
  func.service = service;
  func.proc = proc;
  return func;
}
