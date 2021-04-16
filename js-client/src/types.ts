export class RpcError extends Error {
  constructor(public code: string, message?: string) {
    super(message);

    // https://github.com/Microsoft/TypeScript-wiki/blob/master/Breaking-Changes.md#extending-built-ins-like-error-array-and-map-may-no-longer-work
    Object.setPrototypeOf(this, RpcError.prototype);
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

export type RpcInvoker<TArgs extends any[], TReturn> = {
  (...args: TArgs): RpcInvocation<TReturn>;
  service: string;
  proc: string;
}
