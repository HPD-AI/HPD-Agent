# ASP.NET OpenAPI Contracts

Use `HPD-Agent.AspNetCore.OpenApi` to publish the hosted HPD Agent API as an
OpenAPI 3.1 contract. The package generates the JSON and YAML documents at
runtime; it does not host Swagger UI, Scalar, or ReDoc.

This package publishes HPD's own API. To consume an external REST API as agent
tools, use [OpenAPI Tools](../tools/openapi-tools.md).

The package targets .NET 10.

## Install

```bash
dotnet add package HPD-Agent.AspNetCore.OpenApi --version 0.9.5
```

## Registration

```csharp
using HPD.Agent;
using HPD.Agent.AspNetCore;
using HPD.Agent.AspNetCore.OpenApi;

builder.Services.AddHPDAgentHost("assistant");
builder.Services.AddHPDAgentOpenApi();

var app = builder.Build();

app.MapHPDAgentOpenApiDocuments();
app.MapHPDAgentHostApi("assistant");
```

The canonical documents are available at:

```text
/openapi/hpd-agent-v1.json
/openapi/hpd-agent-v1.yaml
```

Use `MapHPDAgentOpenApiDocuments(routePrefix)` to change the shared prefix, or
map a single document with `MapHPDAgentOpenApi(pattern)`.

## Contract Content

The generated document includes:

- stable operation IDs for hosted routes;
- session, thread, content, execution, and runtime-state schemas;
- the public input union;
- permission, clarification, continuation, and client-tool response shapes;
- multipart content upload and binary download;
- committed resumable SSE with `after` and `Last-Event-ID` cursor contracts;
- the application event union, including custom output events;
- event durability, required-durable, contract-digest, and module metadata;
- client-tool provider WebSocket metadata;
- the immutable agent event catalog;
- evaluation and multi-agent routes when those packages are mapped.

The event union comes from the application event composition. An event type that
is not part of that composition cannot be represented in the contract.

## Authentication

HPD does not choose an ASP.NET Core authentication scheme. The generated document
therefore does not invent security requirements. Add the scheme and operation
requirements that match the application's authentication middleware:

```csharp
using Microsoft.OpenApi;

builder.Services.AddHPDAgentOpenApi(configure: options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

        return Task.CompletedTask;
    });
});
```

Apply the matching authorization policy to the mapped endpoint groups. A security
scheme in the document does not enforce authorization by itself.

## Customize The Document

`AddHPDAgentOpenApi` accepts standard ASP.NET Core `OpenApiOptions` after HPD's
transformers are registered:

```csharp
builder.Services.AddHPDAgentOpenApi(configure: options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Description =
            "Tenant-scoped assistant API.";
        return Task.CompletedTask;
    });

    options.ShouldInclude = description =>
        description.RelativePath?.StartsWith("agents/") == true;
});
```

Use `ShouldInclude` carefully. Removing operations changes the generated client
surface and can remove required recovery operations.

## Client Generation

Use the [HPD C# client generator](https://github.com/HPD-AI/HPD-AI-Framework/blob/main/dotnet/HPD-Agent.Framework/src/HPD-Agent.ClientGenerator/README.md)
for the canonical hosted contract when the client needs:

- typed agent input and response unions;
- typed live events and cursor validation;
- resumable SSE reconnect behavior;
- client-tool WebSocket connection factories;
- multipart and binary clients;
- generated API-change gates.

Generic OpenAPI 3.1-capable generators such as Kiota or OpenAPI Generator can
consume the contract, but they do not automatically reproduce HPD's cursor,
rebase, event-union, or client-tool runtime behavior.

## Operational Notes

- Generate the document from the same endpoint map used by the application.
  Probe-only routes produce valid OpenAPI but do not describe the real runtime.
- Treat `/openapi/hpd-agent-v1.json` as the canonical machine contract and YAML
  as an equivalent representation.
- Hosted operation IDs and route names are part of the documented `0.9.5` contract.
- A security scheme must be updated with the deployment's actual authentication
  boundary and token issuer.
- The application event composition must be available before the document is
  generated.
- `HPDAgentOpenApiExtensions` is the only exported package type. HPD's document,
  operation, and schema transformers are internal and are configured through the
  standard `OpenApiOptions` pipeline.
