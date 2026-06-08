# OpenAPI Tools

OpenAPI specs are external tool sources. HPD Agent reads operations from a JSON OpenAPI document and exposes them as model-callable functions.

## Load A Local JSON Spec

```csharp
using HPD.Agent;
using HPD.Agent.OpenApi;

var specPath = Path.Combine(AppContext.BaseDirectory, "openapi.json");

var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithOpenApi("todo", specPath, config =>
    {
        config.ServerUrlOverride = new Uri("https://example.invalid");
        config.RequiresPermission = true;
    })
    .BuildAsync();
```

Use JSON OpenAPI files. YAML input is not supported by this loader; convert YAML specs to JSON before registration.

## Prefixes And Permissions

The first argument, such as `"todo"`, prefixes generated operation names. Use prefixes to avoid collisions when an agent loads multiple specs.

Set `RequiresPermission` when operations call external systems or mutate state.

## Harness-Owned OpenAPI Sources

A generated harness can contribute an OpenAPI source:

```csharp
using HPD.Agent;
using HPD.Agent.OpenApi;

public sealed class ExternalTools
{
    [OpenApi(Prefix = "todo")]
    public OpenApiConfig TodoApi() => new()
    {
        SpecPath = "openapi.json",
        RequiresPermission = true,
    };
}
```

The generated method returns config. The OpenAPI operations become the model-facing tools.

## Boundary

Do not wrap the current `Agent` build sample in `await using`. Use a normal local variable unless the public lifetime contract changes.
