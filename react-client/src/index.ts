import {
  createElement,
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";

import {
  RpcDispatcher,
  RpcError,
  rpcErrorCodes,
  RpcInvocation,
  RpcReturnType,
} from "cooke-rpc";

export * from "cooke-rpc";

export interface RpcHookOptions {
  throwOnError?: boolean;
}

export const RpcDispatcherContext = createContext<RpcDispatcher>(() => {
  throw new Error("RPC Client not configured");
});

export const RpcDispatcherProvider = (props: {
  dispatcher: RpcDispatcher;
  children: React.ReactNode;
}) =>
  createElement(RpcDispatcherContext.Provider, {
    value: props.dispatcher,
    children: props.children,
  });

export function useRpcDispatcher() {
  return useContext(RpcDispatcherContext);
}

export function useRpc<TRpc extends (...args: any[]) => RpcInvocation<any>>(
  action: TRpc,
  options?: RpcHookOptions
): RpcHookResult<TRpc> {
  const [invoking, setInvoking] = useState(false);
  const [error, setError] = useState<RpcError | undefined>(undefined);
  const [result, setResult] = useState<RpcReturnType<TRpc> | undefined>(
    undefined
  );
  const rpcDispatcher = useContext(RpcDispatcherContext);

  const invoke = useMemo(
    () => async (...args: Parameters<TRpc>): Promise<RpcReturnType<TRpc>> => {
      setInvoking(true);
      setError(undefined);
      try {
        const invocation = action(...args);
        const returnValue = await rpcDispatcher(invocation);
        setResult(returnValue);
        return returnValue;
      } catch (error) {
        const rpcError =
          error instanceof RpcError
            ? error
            : new RpcError(rpcErrorCodes.networkError);
        setError(rpcError);

        if (options?.throwOnError) {
          throw rpcError;
        }

        return new Promise(() => {});
      } finally {
        setInvoking(false);
      }
    },
    [rpcDispatcher, action, options?.throwOnError]
  );

  return {
    invoking,
    error,
    result,
    invoke,
    // Useful for optimistic updates
    setResult,
    setError,
  };
}

// Would be nice to infer this from function but it does not seem to be possible
export type RpcHookResult<
  TRpc extends (...args: any[]) => RpcInvocation<any>
> = {
  invoking: boolean;
  error?: RpcError;
  result?: RpcReturnType<TRpc>;
  invoke: (...args: Parameters<TRpc>) => Promise<RpcReturnType<TRpc>>;
  setResult: React.Dispatch<
    React.SetStateAction<RpcReturnType<TRpc> | undefined>
  >;
  setError: React.Dispatch<React.SetStateAction<RpcError | undefined>>;
};

export function useRpcFetch<
  TRpc extends (...args: any[]) => RpcInvocation<any>
>(action: TRpc, ...args: Parameters<TRpc>): RpcFetchHookResult<TRpc> {
  const [fetched, setQueried] = useState(false);
  const [willFetch, setWillFetch] = useState(true);
  const {
    invoking,
    result: returnValue,
    error,
    invoke,
    setResult,
    setError,
  } = useRpc(action);

  useEffect(
    () => {
      invoke(...args);
      setQueried(true);
      setWillFetch(false);
    },
    // eslint-disable-next-line
    args
  );

  return {
    fetching: invoking || willFetch,
    result: returnValue,
    error,
    refetch: invoke,
    fetched,
    setResult,
    setError,
  };
}

export type RpcFetchHookResult<
  TRpc extends (...args: any[]) => RpcInvocation<any>
> = {
  fetching: boolean;
  error?: RpcError;
  result?: RpcReturnType<TRpc>;
  refetch: (...args: Parameters<TRpc>) => Promise<RpcReturnType<TRpc>>;
  fetched: boolean;
  setResult: React.Dispatch<
    React.SetStateAction<RpcReturnType<TRpc> | undefined>
  >;
  setError: React.Dispatch<React.SetStateAction<RpcError | undefined>>;
};
