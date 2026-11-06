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

Use `WithoutHarnessCollapsing()` when the model should see member functions directly:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<MathTools>()
    .WithoutHarnessCollapsing()
    .BuildAsync();
```

When collapsing is disabled, container functions are removed during build and member functions are exposed directly.

## Customize Collapsing

Collapsing is already enabled, so customization does not require an enable call. Use
`ConfigureHarnessCollapsing(...)` to change its options:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<MathTools>()
    .ConfigureHarnessCollapsing(options =>
    {
        options.NeverCollapse.Add(nameof(MathTools));
    })
    .BuildAsync();
```

## Serialized Agent Configuration

Collapsing belongs to the durable `AgentConfig`. Omitting `collapsing` uses the enabled
default, including when the config is loaded from JSON or used to build a subagent. To
opt out in a stored definition, set `enabled` explicitly:

```json
{
  "collapsing": {
    "enabled": false
  }
}
```

Do not set `collapsing` to `null`; omit it to use the default. Each subagent uses the
collapsing settings in its own `AgentConfig`, so a child can independently keep the
default, customize it, or opt out.

`AgentRunConfig` does not configure collapsing. It controls one invocation, while
collapsing defines the agent's tool visibility and expansion-gated middleware. Runtime tools inherited or
added for a run still pass through the agent's configured visibility and collapsing
rules.

## Runtime Model

Collapsing is runtime visibility behavior, not a one-time source-generation rewrite.

`ContainerMiddleware` filters `ChatOptions.Tools` before each model iteration. It starts from the full initial tool list, then asks `ToolVisibilityManager` which tools should be visible for the current expanded-container state.

When a container is called, `ContainerMiddleware` prepares the visibility
transition and captures active instructions. The neutral
`ToolHarnessExecutionMiddleware` activates any expansion-gated harness pipeline
before the container state commits. After the commit, the execution router
publishes the expansion hook. On the next model iteration,
`ToolVisibilityManager` exposes the permitted member functions owned by the
expanded container. Visibility is projected from the complete function
registry, so an availability filter does not permanently discard functions
while the harness is collapsed. The exposed members are the original
`AIFunction` instances, not bare replacements; they retain their metadata,
execution delegates, and normal global middleware path.

## Recovery Behavior

Hidden function recovery is enabled by default. If a model calls a hidden child function such as `Add` before expanding `MathTools`, middleware can recover by expanding the parent container. Qualified calls such as `MathTools.Add` are also handled as recovery cases.

Recovery is a safety net. Prompts and descriptions should still make the container path clear.

## Harness Middleware

Collapsed harnesses can activate scoped middleware when the container expands:

```csharp
[Collapse("Database operations", Middlewares = [typeof(DbAuditMiddleware)])]
public sealed class DatabaseTools
{
    [AIFunction]
    public string Query(string sql) => "result";
}
```

The source generator can create execution-owned middleware from supported parameterless, configuration, or agent-resource plus configuration constructor shapes.

Middleware that must run before expansion uses the standalone declaration:

```csharp
[Collapse("Database operations")]
[ToolHarnessMiddleware(typeof(DbAuthorizationMiddleware))]
public sealed class DatabaseTools
{
    [AIFunction]
    public string Query(string sql) => "result";
}
```

This middleware is eager for each accepted input even while the functions are
hidden behind the container. A harness can also declare both eager and
expansion-gated middleware with distinct types. The eager group is active from
input admission; the expansion group is appended to the same pipeline when the
container expands. See
[ToolHarness Middleware](../middleware/tool-harness-middleware.md).

For a declared middleware type that needs an explicit activation, use the exact-type override:

```csharp
.WithToolHarness<DatabaseTools>(options =>
    options.OverrideMiddleware<DbAuditMiddleware>(context =>
        ToolHarnessMiddlewareActivation.ExecutionOwned(
            new DbAuditMiddleware(
                context.GetRequiredService<IAuditLog>()))))
```

`OverrideMiddleware<TMiddleware>` replaces a middleware type already declared by the harness. It does not append an undeclared middleware.

Middleware-owned settings can also bind directly to a section of the harness configuration:

```csharp
public sealed class DatabaseRateLimitMiddleware(
    [HarnessConfigurationSection(nameof(DatabaseToolsConfig.RateLimit))]
    DatabaseRateLimitSettings settings)
    : IToolHarnessMiddleware
{
}
```

The section must exist in the owning `IHarnessConfig<T>` contract and have the same type, or an explicitly declared projection. Consumers then set the harness section rather than the middleware type name.

Expansion-gated pipelines are instantiated when the container expands. Eager
harness pipelines are instantiated at accepted-input admission. Both route
iteration, tool-execution, function, parallel-batch, wrapping,
turn-completion, and error hooks through the harness-local pipeline. Global
agent middleware remains active; the harness pipeline is an additional layer
closest to functions owned by that harness. A subagent may receive only an
allowlisted subset of the expanded harness, but those visible functions still
use the same harness middleware pipeline.

The source generator emits codecs for settings nested in the owning immutable harness contract. Do not create a second JSON context for the same typed section.

Do not provide the same settings through both the typed harness section and legacy `MiddlewareConfigs`; that conflict fails before activation.

## Persistence Caveat

The config surface includes `PersistSystemPromptInjections`, but current turn-end behavior clears active container instructions at the end of the message turn. Do not document persistent cross-turn injected system prompts until that behavior is clarified.
