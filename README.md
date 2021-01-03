# Cooke RPC

A set of libraries and tools for easy RPC (Remote Procedure Call) implementation on .NET (Core) and consumption from JS and TypeScript.

## Key features

- Code first (schema generated)
- Generated schema (allows generating clients)
- Polymorphic (de)serialization via JSON (System.Text.Json) or MessagePack

## Alternative techniques and comparisons

Some relevant alternative technologies with potentially main drawbacks compared to Cooke RPC:

- GraphQL with [Hot Chocolate](https://github.com/ChilliCream/hotchocolate) and [Apollo Client](https://github.com/apollographql/apollo-client)
  - Querying requires explicit declaration of all fields to fetch
  - No support for input union types
- ASP.NET Core (Web API) + Open API/Swagger
  - Http only (no direct consumption for instance via web sockets)
  - Generic / less opiniated
- GRPC with [GRPC-DOTNET](https://github.com/grpc/grpc-dotnet)
  - Requires HTTP2
  - Protobuf

## Backend usage with ASP.NET Core

Install nuget package:

```bash
dotnet add package CookeRpc.AspNetCore
```

Add required services:

```c#
protected override void ConfigureServices(IServiceCollection services)
{
    services.AddCookeRpc();
}
```

Plugin middleware (defaults to path "/rpc"):

```c#
protected override void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app.UseCookeRpc();
}
```

Implement service:

```c#
[RpcService]
[Authorize] // If an authenticated user is required
public class MyService
{
    public async Task<int> GetPi() {
        return Math.PI;
    }

    public async Task<int> Add(int left, int right) {
        return left + right;
    }
}
```

## Front-end usage with TypeScript

Install npm packages:

```bash
npm add cooke-rpc
npm add -D cooke-rpc-tooling
```

Generate types and procedures:

```bash
cooke-rpc generate https://localhost:5001/rpc ./src/generated/rpc.ts
```

Usage:

```typescript
import { sendJsonRpc } from "cooke-rpc";
import { myService } from "./generated/rpc";

const result = await sendJsonRpc(myService.add(1, 2), async (json: string) => {
  // Library consumer needs to implement actual transmission
  const response = await fetch("https://localhost:5001/rpc", {
    method: "POST",
    mode: "cors",
    credentials: "include",
    body: json,
  });

  return await response.text();
});
```

### Usage from React

Create RPC client and add to context:

```tsx
import React from "react";
import { render } from "react-dom";

import { RpcClientProvider, createClient } from "cooke-rpc-react";

function App() {
  const [rpcDispatcher] = useState<RpcDispatcher>(
    () => (invocation: RpcInvocation<any>) =>
      sendJsonRpc(invocation, async (json: string) => {
        // Library consumer needs to implement actual transmission
        const response = await fetch(config.serverUrl + "/rpc", {
          method: "POST",
          mode: "cors",
          credentials: "include",
          body: json,
        });

        return await response.text();
      })
  );
  return (
    <RpcDispatcherProvider dispatcher={rpcDispatcher}>
      <div>Your app goes here</div>
    </RpcDispatcherProvider>
  );
}

render(<App />, document.getElementById("root"));
```

Use RPC:

```tsx
import { useRpc, useRpcFetch } from "cooke-rpc-react";
import { myService } from "./generated/rpc";

export const AddComponent = (props: { left: number; right: number }) => {
  const { error, invoke, invoking, result, setResult } = useRpc(
    myService.add,
    props.left,
    props.right
  );
  return (
    <button onClick={invoke}>Calculate result: {result ?? "no result"}</button>
  );
};

export const PiComp = () => {
  // Instantly invokes the RPC, useful for data fetching
  const { error, refetch, fetching, result } = useRpcFetch(myService.getPi);
  return <div>{result ?? "Fetching PI"}</div>;
};
```
