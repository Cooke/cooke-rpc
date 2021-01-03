export class RpcError extends Error {
  constructor(public code: string, message?: string) {
    super(message);
  }
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export type RpcInvocation<TReturn> = {
  service: string;
  proc: string;
  args: any[];
};

export type RpcReturnType<
  TRpc extends (...args: any[]) => RpcInvocation<any>
> = TRpc extends (...args: any[]) => RpcInvocation<infer R> ? R : any;

export interface RpcDispatcher {
  <TResult>(invocation: RpcInvocation<TResult>): Promise<TResult>;
}
