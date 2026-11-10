# OpenAPI Tools

Use `HPD-Agent.OpenApi` to turn operations from an OpenAPI document into
model-callable agent tools. Imported operations participate in the same tool
visibility, permissions, retry, and response projection system as native tools.

This package consumes an external API contract. To publish HPD Agent's own HTTP
API as OpenAPI 3.1, use
[ASP.NET OpenAPI Contracts](../hosting/openapi-contract.md).

The package targets .NET 8, .NET 9, and .NET 10.

## Install

```bash
dotnet add package HPD-Agent.OpenApi --version 0.9.5
dotnet add package HPD-Agent.Providers.OpenAI --version 0.9.5
```

`HPD-Agent.OpenApi` brings in `HPD-Agent.Framework`. Install a provider package or
configure an `IChatClient` directly.

## Load A Local Spec

```csharp
using HPD.Agent;
using HPD.Agent.OpenApi;

var specPath = Path.Combine(AppContext.BaseDirectory, "openapi.json");

await using var agent = await new AgentBuilder(new AgentConfig
{
    Name = "todo-agent"
})
    .WithChatClient(chatClient)
    .WithOpenApi("todo", specPath, config =>
    {
        config.ServerUrlOverride = new Uri("https://api.example.com/");
        config.OperationsToExclude = ["deleteTodo"];
        config.RequiresPermission = true;
    })
    .BuildAsync();

var sessionId = await agent.CreateSessionAsync();
var result = await agent.RunAsync("List the open TODOs.", sessionId);
```

The package supports JSON and YAML OpenAPI documents. Exactly one of `SpecPath`
or `SpecUri` must be configured.

An operation named `listTodos` from source `todo` becomes
`todo_listTodos`.

## Load A Remote Spec

```csharp
var config = new OpenApiConfig
{
    SpecUri = new Uri("https://api.example.com/openapi.json"),
    ServerUrlOverride = new Uri("https://api.example.com/")
};

await using var agent = await new AgentBuilder(agentConfig)
    .WithChatClient(chatClient)
    .WithOpenApi("example", config)
    .BuildAsync();
```

HPD creates and owns an `HttpClient` when one is not supplied. Supply your own
client for custom handlers, proxies, certificates, or application-owned lifetime:

```csharp
var config = new OpenApiConfig
{
    SpecPath = "openapi.json",
    HttpClient = applicationOwnedHttpClient
};
```

An application-provided `HttpClient` is not disposed by HPD.

## Authentication

`AuthCallback` runs before each request. Resolve secrets inside the callback so
rotation and refresh do not require rebuilding the agent:

```csharp
var config = new OpenApiConfig
{
    SpecUri = new Uri("https://api.example.com/openapi.json"),
    AuthCallback = async (request, cancellationToken) =>
    {
        var token = await secrets.RequireAsync(
            "example:ApiToken",
            "Example API",
            cancellationToken: cancellationToken);

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);
    }
};
```

Use `ErrorDetector` when an API reports application errors through HTTP 200:

```csharp
config.ErrorDetector = (response, body) =>
{
    if (body is not null &&
        body.Contains("\"ok\":false", StringComparison.OrdinalIgnoreCase))
    {
        return new HPD.OpenApi.Core.OpenApiErrorResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = body
        };
    }

    return null;
};
```

## Request Bodies And Schemas

By default, body properties are flattened into tool arguments. Set
`EnableDynamicPayload = false` to expose the request body as one JSON string:

```csharp
config.EnableDynamicPayload = false;
```

Set `EnablePayloadNamespacing = true` when flattened nested payload names need
parent-property prefixes to avoid collisions.

`SchemaTransformOptions` applies `AIJsonUtilities.TransformSchema` after OpenAPI
schema conversion:

```csharp
config.SchemaTransformOptions = new AIJsonSchemaTransformOptions
{
    DisallowAdditionalProperties = true
};
```

## Operation Selection

Choose one filtering mechanism per source:

```csharp
config.OperationsToExclude = ["deleteTodo", "replaceTodo"];
```

or:

```csharp
config.OperationSelectionPredicate = operation =>
    !string.Equals(operation.Method, "DELETE", StringComparison.OrdinalIgnoreCase);
```

`OperationsToExclude` and `OperationSelectionPredicate` are mutually exclusive.
A source that produces zero operations fails agent construction with:

```text
OpenApiSourceProducedNoOperations
```

The failure reports the source name, remaining operation count, exclusions, and
whether a predicate is configured. If an empty source is intentional:

```csharp
config.AllowEmptyCatalog = true;
```

A malformed operation is skipped without preventing the remaining operations
from loading. HPD emits a structured warning with the source name, operation ID,
HTTP method, path, and reason. Full exception detail goes through the configured
`ILogger`.

## Permissions And Invocation

OpenAPI tools do not require permission by default. Enable permission for an
entire source when operations mutate state or reach sensitive systems:

```csharp
config.RequiresPermission = true;
```

The generated function receives the normal HPD function permission contract and
can be authorized or denied through standard permission middleware.

Retryable HTTP failures (`401`, `408`, `429`, and `5xx`) enter the function retry
path. OpenAPI functions automatically use HPD's OpenAPI error handler for status
categorization and `Retry-After` delays. Client errors such as `400`, `404`, and
`422` are returned as structured model-facing errors so the model can correct its
arguments.

## Response Optimization

Reduce the API payload returned to the model:

```csharp
config.ResponseOptimization = new ResponseOptimizationConfig
{
    DataField = "result.items",
    FieldsToInclude = ["id", "name", "status"],
    MaxLength = 8_000
};
```

Transforms run in this order:

1. extract `DataField`;
2. include `FieldsToInclude` or exclude `FieldsToExclude`;
3. truncate to `MaxLength`.

The cross-field transformation is applied to arrays of objects as well as single
objects. The default response limit is 4,000 characters.

## Collapsing

Set `CollapseWithinToolHarness = true` to expose operations behind an
`OpenApi_{source}` activation container. Expanding the container reveals the
operation tools.

```csharp
config.CollapseWithinToolHarness = true;
```

Without collapsing, imported operations are top-level tools or flat members of
their owning ToolHarness.

## Harness-Owned OpenAPI Sources

A generated ToolHarness can contribute an OpenAPI source:

```csharp
using HPD.Agent;
using HPD.Agent.OpenApi;
using HPD.Agent.Secrets;

public sealed class ExternalTools(ISecretResolver secrets)
{
    [OpenApi(Prefix = "todo")]
    [RequiresPermission]
    public OpenApiConfig TodoApi() => new()
    {
        SpecPath = "openapi.json",
        AuthCallback = async (request, cancellationToken) =>
        {
            var token = await secrets.RequireAsync(
                "todo:ApiToken",
                "Todo API",
                cancellationToken: cancellationToken);

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }
    };
}
```

The attributed method must be parameterless and return `OpenApiConfig`.
Constructor injection supplies dependencies to the harness instance.

## Native AOT And Public API

The package is Native AOT-compatible. Use source-generated composition and the
builder or `[OpenApi]` registration paths.

Application code should depend on `WithOpenApi`, `OpenApiConfig`, and
`ResponseOptimizationConfig`. Specification parsing, operation loading, function
construction, response formatting, and error classification are internal.

## Troubleshooting

- No tools: inspect the `OpenApiSourceProducedNoOperations` failure,
  `OperationsToExclude`, `OperationSelectionPredicate`, and operation IDs in the
  source document.
- Missing server URL: set `ServerUrlOverride` or provide an absolute server URL
  in the OpenAPI document.
- Authentication changes require a rebuild: move secret resolution into
  `AuthCallback`.
- A successful HTTP response contains an application error: configure
  `ErrorDetector`.
- Body arguments are too dynamic: set `EnableDynamicPayload = false` and pass a
  JSON `payload` string, or define a tighter OpenAPI schema.
