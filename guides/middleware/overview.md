# Middleware Overview

Middleware is how you add cross-cutting behavior to an agent run without putting that behavior inside every tool, prompt, or model call. Use it for logging, policy checks, request shaping, retries, timeouts, event requests, safety gates, thread/session state updates, and result formatting.

For built-in logging, telemetry, and usage-aware middleware decisions, see [Logging And Telemetry](../observability/logging-and-telemetry.md).

The main DX question is not only "how do I write middleware?" It is "where should this middleware attach?"

## Choose Where It Attaches

| Use this when | Attach middleware here |
| --- | --- |
| Every run of this built agent should get the behavior | `AgentBuilder.WithMiddleware(...)` |
| Only one call to `RunAsync` should get the behavior | `AgentRunConfig.RuntimeMiddleware` |
| The behavior should come from JSON or hosted agent config | `middlewares` in `AgentConfig` |
| Every selected ToolHarness input should get the behavior, even before expansion | `[ToolHarnessMiddleware]` |
| The behavior only matters while a collapsed ToolHarness is expanded | `[Collapse(Middlewares = ...)]` |
| The behavior is a built-in product feature | A builder helper such as `WithPermissions(...)`, `WithCircuitBreaker(...)`, or error-handling helpers |

Start with the smallest attachment point that matches the behavior. If the middleware is just temporary telemetry for one request, do not make it part of the agent definition. If it protects the whole agent, register it on the builder or in config. If it protects one tool harness, scope it to that harness.

Memory and context enrichment usually starts as ordinary middleware. Retrieve context before the model turn, add it to the thread history for that turn, then store any small memory pointers or policy state through middleware state after the turn. Keep raw documents, embeddings, and large memory payloads in an application store or content store.

## Agent Builder

Builder registration is the usual path for app code. The middleware becomes part of the agent's default pipeline when the agent is built.

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are concise.")
    .WithMiddleware(new RequestLoggingMiddleware())
    .WithMiddleware(new RateLimitMiddleware(limitPerMinute: 60))
    .BuildAsync();
```

You can also register a middleware type with a parameterless constructor:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithMiddleware<RequestLoggingMiddleware>()
    .BuildAsync();
```

Builder-registered middleware is reusable and testable because dependencies can be passed in directly or resolved through the builder's service provider.

## Runtime Middleware

Use runtime middleware when a single run needs extra behavior that should not become part of the agent definition. `RuntimeMiddleware` is C# only and is ignored by JSON serialization.

```csharp
var result = await agent.RunAsync(
    "Summarize this trace.",
    new AgentRunConfig
    {
        RuntimeMiddleware =
        [
            new TraceOnlyThisRunMiddleware(traceId)
        ]
    });
```

Runtime middleware is applied outside the configured middleware for that run. That means its `Before*` hooks run before the agent's configured middleware, and its `After*` hooks run after the configured middleware.

## Agent Config

Use config registration when middleware belongs to a deployable agent definition. Config middleware is resolved through the generated middleware registry at build time, so custom middleware must be available to the app and marked with `[Middleware]`.

```csharp
[Middleware("RateLimit")]
public sealed class RateLimitMiddleware : IAgentMiddleware
{
    public RateLimitMiddleware() { }

    public RateLimitMiddleware(RateLimitConfig config)
    {
        // Store options from the JSON "config" object.
    }
}
```

If you do not pass a custom name to `[Middleware]`, use the class name in config.

Simple config:

```json
{
  "middlewares": [
    "RequestLoggingMiddleware",
    "RateLimit"
  ]
}
```

Config with middleware-specific options:

```json
{
  "middlewares": [
    "RequestLoggingMiddleware",
    {
      "name": "RateLimit",
      "config": {
        "requestsPerMinute": 60
      }
    }
  ]
}
```

Middleware listed in config is resolved in the order it appears in the `middlewares` array.

## Tool-Harness Scoped Middleware

ToolHarness middleware runs in an execution-local pipeline owned by one
selected harness. It is narrower than global agent middleware and can use the
harness's DI, configuration, and Agent resources.

Choose its activation policy deliberately:

| Policy | Declaration | Activation |
| --- | --- | --- |
| Expansion-gated | `[Collapse(Middlewares = ...)]` | When the container expands |
| Eager | `[ToolHarnessMiddleware]` | At accepted-input admission, regardless of visibility |

Eager middleware is appropriate for authorization, audit, policy, context
setup, and telemetry that must run before tools become visible.

Declare expansion-gated middleware:

```csharp
[Collapse(Middlewares = [typeof(DatabaseAuditMiddleware)])]
public sealed class DatabaseToolHarness
{
    // Tool functions live here.
}
```

Declare eager middleware:

```csharp
[ToolHarnessMiddleware(typeof(DatabaseAuthorizationMiddleware))]
public sealed class DatabaseToolHarness
{
    // Tool functions live here.
}
```

Eager middleware can preserve normal collapsing by using a description-only
`[Collapse]`:

```csharp
[Collapse("Database operations")]
[ToolHarnessMiddleware(typeof(DatabaseAuthorizationMiddleware))]
public sealed class DatabaseToolHarness;
```

Both policies may coexist with different middleware types. Eager middleware is
active from accepted-input admission; expansion-gated middleware is appended
when the harness expands. A single middleware type cannot use both policies.
See [ToolHarness Middleware](tool-harness-middleware.md) for hooks and
lifecycle.

Use an exact-type activation override when declared middleware needs services:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<DatabaseToolHarness>(opts =>
        opts.OverrideMiddleware<DatabaseAuditMiddleware>(context =>
            ToolHarnessMiddlewareActivation.ExecutionOwned(
                new DatabaseAuditMiddleware(
                    context.GetRequiredService<IAuditLog>()))))
    .BuildAsync();
```

The override replaces the middleware at its declared position. It cannot add an undeclared type.

Middleware-owned settings belong to the harness's immutable configuration:

```csharp
public sealed class DatabaseRateLimitMiddleware(
    [HarnessConfigurationSection(nameof(DatabaseToolHarnessConfig.RateLimit))]
    DatabaseRateLimitSettings settings)
    : IToolHarnessMiddleware
{
}
```

Consumers configure that section:

```json
{
  "toolHarnesses": [
    {
      "name": "DatabaseToolHarness",
      "config": {
        "rateLimit": {
          "requestsPerMinute": 20
        }
      }
    }
  ]
}
```

`middlewareConfigs` remains available for legacy middleware without a typed harness section. Do not configure the same middleware through both paths.

## Built-In Helpers

Some middleware is easier to add through feature helpers than by constructing the middleware yourself. Prefer the helper when it exists because it usually wires the matching options and state correctly.

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithPermissions()
    .WithCircuitBreaker(maxConsecutiveCalls: 3)
    .BuildAsync();
```

Use the focused guide for the feature you are enabling:

- [Permissions Middleware](permissions.md)
- [Error Handling Middleware](error-handling.md)
- [Middleware State Persistence](state-persistence.md)

`PermissionMiddleware` is the framework call gate. It evaluates the registered
function or action through a trusted `IPermissionPolicy`, validates the exact
choice set, journals the request and resolution, and issues an invocation-bound
grant. Applications customize the decision by selecting a policy descriptor;
they do not replace the request/response protocol. Resource-level checks such as
path, network, or workspace capability use the same shared permission lifecycle
with a different permission domain.

## Events From Middleware

Middleware can emit events when the app needs to observe or respond to what is happening inside the agent loop.

For one-way observability, publish an event from a hook context:

```csharp
public async Task BeforeFunctionAsync(
    BeforeFunctionContext context,
    CancellationToken cancellationToken)
{
    await context.PublishAsync(
        new FunctionAuditEvent(context.Function?.Name),
        cancellationToken);
}
```

When the context has session and thread scope, `PublishAsync(...)` commits the event to the canonical journal before subscribers receive it. Without thread scope, publication is process-local.

For a request/response interaction, emit a bidirectional event and wait for a response. Permissions use this pattern: middleware asks the host whether a sensitive action is allowed, and the host responds through the agent event coordinator.

```csharp
var response = await context.RequestAsync<PermissionRequestEvent, PermissionResponseEvent>(
    request,
    timeout: TimeSpan.FromSeconds(30));
```

In direct in-process code, answer with `agent.AnswerRequestAsync(...)` or `agent.TryAnswerRequestAsync(...)` for the matching response event. In ASP.NET Core hosted clients, read the request from canonical SSE or restore it from `/state`, then send the matching `IResponseEvent` envelope through the active coordinator's `/responses` route. Preserve descendant child scope fields when the parent state surfaced a subagent request.

Use this pattern when the user, UI, policy engine, bot adapter, or hosted runtime needs to make a decision during the run.

## Runtime Context Surfaces

HPD exposes several context surfaces. Choose the narrowest one that matches the job:

| Surface | Use It For |
| --- | --- |
| `AgentRunConfig` | Per-run provider/model selection, harness setting overrides, runtime middleware, tool context instances, attachments, compaction controls, and temporary behavior. |
| Middleware hook context | Turn, iteration, tool, function, thread, event, service, session, and thread data that belongs to the agent scheduler. |
| `[MiddlewareState]` | Private middleware-owned state that should persist by session or thread. |
| `FunctionExecutionContext` | Narrow tool/function access to event emission, bidirectional requests, services, content store, background tasks, struct events, and run metadata. |
| Application storage | Business records, secrets, large documents, embeddings, durable memory bodies, audit archives, and tenant policy. |

Do not mutate scheduler-owned session or thread state from tool bodies. Tool functions receive `FunctionExecutionContext` so they can interact with the runtime without taking over middleware state management.

## Order And Lifecycle

Registration order matters. For `Before*` hooks, middleware runs in registration order. For `After*` hooks and error notification, middleware runs in reverse order. Wrapper hooks form nested calls around the model or function body.

For the full hook-by-hook model, read [Middleware Lifecycle](../../concepts/middleware-lifecycle.md). For writing your own middleware, read [Custom Middleware](custom-middleware.md).
