# Collapsing And Containers

Collapsing hides a group of functions behind a container tool. It is enabled by default, but only tools with container metadata actually collapse. For a native harness, add `[Collapse]` to the harness class.

## Create A Collapsed Harness

```csharp
using HPD.Agent;

[Collapse(
    "Math operations",
    FunctionResult = "Math tools are active.",
    SystemPrompt = "Use exact arithmetic and show units when relevant.")]
public sealed class MathTools
{
    [AIFunction]
    public int Add(int a, int b) => a + b;

    [AIFunction]
    public int Multiply(int a, int b) => a * b;
}
```

Register it like any other harness:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<MathTools>()
    .BuildAsync();
```

Before expansion, the model sees a `MathTools` container and any non-collapsed tools. The `Add` and `Multiply` functions are hidden.

After the model calls `MathTools`, the container is hidden and `Add` and `Multiply` become visible on the next model iteration. `FunctionResult` is returned as the container call result. `SystemPrompt` is added to the active container instructions while the container is active.

## Disable Collapsing For An Agent

Use `WithoutToolCollapsing()` when the model should see member functions directly:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<MathTools>()
    .WithoutToolCollapsing()
    .BuildAsync();
```

When collapsing is disabled, container functions are removed during build and member functions are exposed directly.

## Runtime Model

Collapsing is runtime visibility behavior, not a one-time source-generation rewrite.

`ContainerMiddleware` filters `ChatOptions.Tools` before each model iteration. It starts from the full initial tool list, then asks `ToolVisibilityManager` which tools should be visible for the current expanded-container state.

When a container is called, `ContainerMiddleware` records the expanded container, captures its active instructions, and creates any scoped middleware pipeline for that harness. On the next model iteration, `ToolVisibilityManager` exposes the member functions owned by that expanded container.

## Recovery Behavior

Hidden function recovery is enabled by default. If a model calls a hidden child function such as `Add` before expanding `MathTools`, middleware can recover by expanding the parent container. Qualified calls such as `MathTools.Add` are also handled as recovery cases.

Recovery is a safety net. Prompts and descriptions should still make the container path clear.

## Scoped Middleware

Collapsed harnesses can activate scoped middleware when the container expands:

```csharp
[Collapse("Database operations", Middlewares = [typeof(DbAuditMiddleware)])]
public sealed class DatabaseTools
{
    [AIFunction]
    public string Query(string sql) => "result";
}
```

The source generator can create factories for middleware with public parameterless constructors or a single public `*Config` or `*Options` constructor. Middleware that needs DI should be supplied through `WithToolHarness<T>(options => options.AddScopedMiddleware(...))`.

Scoped pipelines route function, iteration, batch, and error hooks for functions owned by that harness after the container has expanded.

If scoped middleware uses a config constructor and that config is supplied from JSON, register JSON metadata for the config type:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(DbAuditMiddlewareConfig))]
public partial class DatabaseToolsJsonContext : JsonSerializerContext
{
}
```

This is the same AOT contract used by tool harness constructor config. The generated factory discovers the middleware config type; the JSON context makes hydration reflection-free.

## Persistence Caveat

The config surface includes `PersistSystemPromptInjections`, but current turn-end behavior clears active container instructions at the end of the message turn. Do not document persistent cross-turn injected system prompts until that behavior is clarified.
