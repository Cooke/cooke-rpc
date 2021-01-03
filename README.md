# Cooke RPC (Remote Procedure Call)
A set of libraries and tools for easy procedure implementation on .NET (Core) and consumption from TypeScript. 

## Key features
- Code first (schema generated)
- Generated schema (allows generating clients)
- Polymorphic (de)serialization (JSON via System.Text.Json or MessagePack)

## Alternatives and comparison
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
```
dotnet add package CookeRpc.AspNetCore
```

Add required services:
```
protected override void ConfigureServices(IServiceCollection services)
{
    services.AddCookeRpc();
}
```

Plugin middleware (defaults to path "/rpc"):
```
protected override void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app.UseCookeRpc();
}
```

Implement service:
```
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
```
npm add cooke-rpc
npm add -D cooke-rpc-tooling
```

Generate types and procedures:
```
cooke-rpc generate https://localhost:5001/rpc ./src/generated/rpc.ts
```

Usage:
```
import { dispatchRpc } from "cooke-rpc";
import { myService } from "./generated/rpc";

const result = await dispatchRpc("https://localhost:5001/rpc", myService.add(1, 1));
```

### Usage from React

Create RPC client and add to context:
```
import React from 'react';
import { render } from 'react-dom';

import { RpcClientProvider, createClient } from 'cooke-rpc-react';

function App() {
  const rpcClient = createClient({ url: "https://localhost:5001/rpc" });
  return (
    <RpcClientProvider client={rpcClient}>
      <div>
      </div>
    </ApolloProvider>
  );
}

render(<App />, document.getElementById('root'));
```

Use RPC:
```
import { useRpc, useRpcFetch } from "cooke-rpc-react";
import { myService } from "./generated/rpc";

export const AddComponent = (props: { left: number, right: number} ) => {
  const { error, invoke, invoking, result, setResult } = useRpc(myService.add, props.left, props.right);
  return <button onClick={invoke}>Calculate result: { result ?? "no result" }</button>
}

export const PiComp = () => {
  const { error, refetch, fetching, result } = useRpcFetch(myService.getPi, 1, 1);
  return <div>{result ?? "Fetching PI"}</div>
}
```
