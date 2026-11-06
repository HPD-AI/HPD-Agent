# ToolHarness Middleware

Tool harnesses support two middleware activation policies:

| Declaration | Activation |
| --- | --- |
| `Collapse.Middlewares` | Expansion-gated; activates when the harness container expands |
| `[ToolHarnessMiddleware]` | Eager; activates at accepted-input admission for every selected harness |

Use expansion-gated middleware when it exists only to support tools after
container activation. Use eager middleware for authorization, audit, policy,
context setup, telemetry, or resources that must exist before the harness
becomes visible.

Both declarations produce an execution-local pipeline owned by the harness and
accepted input. Neither creates agent-global middleware.

## Eager Middleware

Declare eager middleware on the harness:

```csharp
using HPD.Agent;
using HPD.Agent.Middleware;

[ToolHarnessMiddleware(typeof(RepositoryAuthorizationMiddleware))]
public sealed class RepositoryTools
{
    [AIFunction]
    public Task<string> ReadAsync(string path) => File.ReadAllTextAsync(path);
}

public sealed class RepositoryAuthorizationMiddleware : IToolHarnessMiddleware
{
    public Task BeforeMessageTurnAsync(
        BeforeMessageTurnContext context,
        CancellationToken cancellationToken)
    {
        // Prepare authorization context before tools become visible.
        return Task.CompletedTask;
    }
}
```

Register the harness normally:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<RepositoryTools>()
    .BuildAsync();
```

Selecting the harness registers its eager middleware descriptors during agent
construction. The middleware instance is not constructed inside `BuildAsync`;
it is created for each accepted input because it may depend on execution-scoped
services and configuration.

Eager middleware activates before the first eligible message-turn or iteration
hook, even when the harness is still collapsed.

## Expansion-Gated Middleware

Use `Collapse.Middlewares` for middleware that belongs to expansion:

```csharp
[Collapse(
    "Repository operations",
    Middlewares = [
        typeof(RepositoryTransactionMiddleware),
        typeof(RepositoryAuditMiddleware)
    ])]
public sealed class RepositoryTools
{
    [AIFunction]
    public Task<string> ReadAsync(string path) => File.ReadAllTextAsync(path);
}
```

The middleware remains inactive while the container is collapsed. It is
constructed and activated as part of a successful expansion transaction. If
activation fails, the visibility expansion does not commit.

## Collapsing With Eager Middleware

Description-only `[Collapse]` can coexist with eager middleware:

```csharp
[Collapse("Repository operations")]
[ToolHarnessMiddleware(typeof(RepositoryAuthorizationMiddleware))]
public sealed class RepositoryTools
{
    [AIFunction]
    public Task<string> ReadAsync(string path) => File.ReadAllTextAsync(path);
}
```

In this configuration:

- the model sees the collapsed `RepositoryTools` container initially;
- `RepositoryAuthorizationMiddleware` is active for the accepted input before
  expansion;
- `OnHarnessExpandedAsync` runs after the expansion commits;
- the exposed member functions use the same harness-local pipeline.

The two policies can also coexist with distinct middleware types:

```csharp
[Collapse(
    "Coding operations",
    Middlewares = [typeof(LanguageServerMiddleware)])]
[ToolHarnessMiddleware(typeof(AuditMiddleware))]
public sealed class CodingToolHarness;
```

`AuditMiddleware` activates at accepted-input admission.
`LanguageServerMiddleware` remains dormant until expansion and is then appended
to the same harness pipeline. The activation order is eager first, deferred
second.

## Without Collapsing

Eager middleware does not depend on collapsing:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<RepositoryTools>()
    .WithoutHarnessCollapsing()
    .BuildAsync();
```

Member functions are visible directly and eager middleware still activates for
each accepted input. Expansion-gated middleware does not run because no
expansion occurs.

## Lifecycle Hooks

`IToolHarnessMiddleware` inherits every `IAgentMiddleware` hook and adds three
harness-specific hooks:

```csharp
public interface IToolHarnessMiddleware : IAgentMiddleware
{
    ValueTask OnHarnessActivatedAsync(
        ToolHarnessActivationContext context,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    ValueTask OnHarnessExpandedAsync(
        ToolHarnessExpansionContext context,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    ValueTask OnHarnessDeactivatingAsync(
        ToolHarnessDeactivationContext context,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
```

The hooks have no-op defaults. Override only the lifecycle points your
middleware needs.

### Activation

`OnHarnessActivatedAsync` runs after all declared middleware instances for the
harness are constructed. Hooks run in declaration order.

The activation context includes:

- the exact harness identity;
- the accepted-input execution identity;
- input child-scope services;
- the effective run configuration;
- session and thread IDs;
- Agent-owned resources.

An activation failure prevents the harness pipeline from publishing.

### Expansion

`OnHarnessExpandedAsync` runs once after a collapsed harness visibility
transition commits:

```csharp
public sealed class RepositoryAuthorizationMiddleware : IToolHarnessMiddleware
{
    public ValueTask OnHarnessExpandedAsync(
        ToolHarnessExpansionContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"{context.HarnessIdentity} expanded because {context.Reason}");

        foreach (var function in context.RevealedFunctions)
            Console.WriteLine($"Revealed: {function}");

        return ValueTask.CompletedTask;
    }
}
```

The expansion reason is one of:

- `ContainerCall`;
- `HiddenFunctionRecovery`;
- `QualifiedFunctionRecovery`;
- `RestoredExpansion`.

The context also includes the expansion call ID when one exists. Directly
visible harnesses do not have an expansion transition and do not receive this
hook.

Expansion is already committed when the hook runs. A hook failure is reported
as an execution diagnostic but does not undo visibility or prevent eventual
cleanup. The hook cannot veto expansion.

### Deactivation

`OnHarnessDeactivatingAsync` runs before disposal in reverse declaration order.
It receives the deactivation reason: completed, failed, cancelled, or shutdown.

New admissions stop before deactivation begins. In-flight admissions drain,
each middleware receives a cleanup opportunity, and execution-owned middleware
is disposed exactly once.

## Lifecycle Sequences

Expansion-gated middleware:

```text
harness selected
  -> agent built
  -> accepted input starts
  -> middleware remains dormant
  -> harness expands
  -> OnHarnessActivatedAsync
  -> visibility commits
  -> OnHarnessExpandedAsync
  -> normal middleware hooks
  -> OnHarnessDeactivatingAsync
  -> dispose
```

Eager middleware:

```text
harness selected
  -> agent built
  -> accepted input starts
  -> OnHarnessActivatedAsync
  -> normal turn and iteration hooks
  -> OnHarnessExpandedAsync if the harness expands
  -> normal function hooks
  -> OnHarnessDeactivatingAsync
  -> dispose
```

The distinction matters: activation means the middleware pipeline exists.
Expansion means the harness's tools became visible.

Mixed eager and deferred middleware:

```text
accepted input starts
  -> activate eager middleware
  -> normal turn and iteration hooks use eager middleware
  -> harness expands
  -> activate deferred middleware
  -> append it after eager middleware
  -> OnHarnessExpandedAsync runs for both
  -> normal hooks use the combined pipeline
  -> deferred middleware deactivates/disposes before eager middleware
```

## Ordering And Isolation

Within one harness:

1. Base-class middleware precedes derived-class middleware.
2. Each declaration preserves its array order.
3. Activation and before hooks run in declaration order.
4. Function wrappers nest in declaration order.
5. After, error, and deactivation hooks unwind in reverse order.

Across selected harnesses, turn and iteration hooks follow harness registration
order and unwind in reverse order. A function executes only its owning
harness's middleware pipeline. Parallel harnesses may execute concurrently.

## Configuration And Ownership

Eager and expansion-gated middleware use the same supported constructor and
ownership contracts:

- parameterless construction;
- a generated configuration or harness-section constructor;
- Agent-owned resource construction;
- explicit child-scope service ownership with
  `ToolHarnessMiddlewareLifetime(Services)`.

```csharp
[ToolHarnessMiddlewareLifetime(ToolHarnessMiddlewareOwnership.Services)]
public sealed class RepositoryAuthorizationMiddleware(
    IAuthorizationContext authorization)
    : IToolHarnessMiddleware
{
}
```

Exact-type overrides keep the generated declaration position:

```csharp
builder.WithToolHarness<RepositoryTools>(options =>
    options.OverrideMiddleware<RepositoryAuthorizationMiddleware>(context =>
        ToolHarnessMiddlewareActivation.ExecutionOwned(
            new RepositoryAuthorizationMiddleware(
                context.GetRequiredService<IAuthorizationContext>()))));
```

`OverrideMiddleware<T>()` replaces a declared middleware type. It does not add
an undeclared middleware.

## Declaration Rules

- A harness must be selected with `WithToolHarness<T>()` before its middleware
  can run.
- Every listed type must implement `IToolHarnessMiddleware`.
- ToolHarness middleware requires source generation.
- Reflection-based registration rejects both middleware declaration styles.
- Do not list the same type twice in one declaration.
- `Collapse.Middlewares` and `[ToolHarnessMiddleware]` may coexist on one
  logical harness when their middleware types are disjoint.
- A middleware type can appear in only one activation policy. Declaring the
  same exact type in both lists is rejected with `HPDAG0214`.
- Eager descriptors precede deferred descriptors in the combined pipeline.

This is valid:

```csharp
[Collapse("Repository operations")]
[ToolHarnessMiddleware(typeof(RepositoryAuthorizationMiddleware))]
public sealed class RepositoryTools;
```

This is also valid:

```csharp
[Collapse(
    "Repository operations",
    Middlewares = [typeof(RepositoryTransactionMiddleware)])]
[ToolHarnessMiddleware(typeof(RepositoryAuthorizationMiddleware))]
public sealed class RepositoryTools;
```

The transaction middleware activates on expansion, while the authorization
middleware has already been active since accepted-input admission.

## Related

- [Middleware Lifecycle](../../concepts/middleware-lifecycle.md)
- [Custom Middleware](custom-middleware.md)
- [Collapsing And Containers](../tools/collapsing-and-containers.md)
- [Configure A Tool Harness](../tools/configure-a-tool-harness.md)
- [Source Generation, AOT, And Trimming](../tools/source-generation-aot-and-trimming.md)
