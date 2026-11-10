# Agent Operations, Background Work, And Notifications

HPD uses `AgentOperation` as the common model for work that can outlive the call that started
it. The same aggregate describes provider background responses, background tool invocations,
subagents, workflows, MCP tasks, and other long-running operations.

There are three related surfaces:

```text
provider background response
  provider owns a long-running model response

runtime background invocation
  HPD starts a function, subagent, or workflow and returns an operation receipt

tool-body operation
  a function explicitly registers its own runtime-owned work
```

These are related, but they are not interchangeable. Choose the surface that owns the work.

## Provider Background Responses

Some chat providers can accept a long-running request, return a continuation token, and expose
the result through polling. Enable that behavior at the agent level:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI("gpt-5-mini")
    .WithBackgroundResponses(config =>
    {
        config.DefaultAllow = true;
        config.AutoPollToCompletion = true;
        config.DefaultPollingInterval = TimeSpan.FromSeconds(3);
        config.DefaultTimeout = TimeSpan.FromMinutes(10);
    })
    .BuildAsync();
```

Provider support is required. With `AutoPollToCompletion = true`, the caller still waits for the
turn result while HPD follows the provider's continuation. With it disabled, HPD can preserve the
provider operation for the runtime and hosted operation projections to observe and control.

Use `AgentRunConfig.BackgroundResponses` to override this default for one run.

## Runtime Background Invocation

Functions, subagents, and multi-agent capabilities can declare an invocation-mode policy:

```csharp
[AIFunction(
    Name = "build_index",
    InvocationModePolicy = AgentInvocationModePolicy.ModelChoice)]
public async Task<string> BuildIndex(string path, CancellationToken cancellationToken)
{
    await BuildIndexAsync(path, cancellationToken);
    return "Index build completed.";
}
```

The policies are:

| Policy | Behavior |
| --- | --- |
| `SynchronousOnly` | Wait for the result before continuing the model turn. |
| `BackgroundOnly` | Register the work and immediately return an operation receipt. |
| `ModelChoice` | Expose `invocationMode` so the model chooses synchronous or background execution. |

Background invocation returns an `AgentOperationReceipt` to the model instead of waiting for the
function or child-agent result. HPD commits an `AgentOperationRegisteredEvent` before the work
starts and records subsequent state through `AgentOperationTransitionedEvent`.

The work needs a runtime with an operation registry. Subagent and other runtime-owned background
invocations therefore belong in started-runtime scenarios such as hosted HTTP/SSE, bot, or TUI
applications.

## Tool-Body Operations

When a synchronous tool call needs to return promptly while keeping work alive, start an operation
from `FunctionExecutionContext`:

```csharp
using HPD.Agent;
using HPD.Agent.Middleware;

public sealed class IndexingTools
{
    [AIFunction(Name = "start_indexing")]
    public async Task<string> StartIndexing(
        FunctionExecutionContext context,
        CancellationToken cancellationToken)
    {
        AgentOperationReceipt receipt = await context.StartOperationAsync(
            "IndexWorkspace",
            new Dictionary<string, string> { ["workspace"] = "default" },
            new AgentOperationNotificationPolicy { IncludeTerminal = true },
            async (operationId, runtimeToken) =>
            {
                await IndexWorkspaceAsync(runtimeToken);
                return new AgentOperationCompletion(
                    Summary: "Workspace index completed.");
            },
            cancellationToken);

        return $"Started indexing operation {receipt.OperationId}.";
    }

    private static Task IndexWorkspaceAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

The tool call returns after HPD commits the operation. The work body receives an operation ID and
a runtime-owned cancellation token. Use `FunctionExecutionContext.CancelOperationAsync(...)` to
request cancellation of an operation that advertises the `Cancel` capability.

Function permission applies before the tool body runs. Background execution does not create a
second permission domain; it is one function call whose result is represented by an operation.

## Operation State

`AgentOperationSnapshot` is the durable control projection. It includes:

- source kind: `LocalTool`, `McpTask`, `SubAgent`, `Workflow`, `MultiAgent`, or `ProviderOperation`
- provider status and HPD observation status
- the owning agent, session, and thread
- the original function invocation when present
- completion, failure, and protected recovery information
- supported controls such as cancel, update, detach, or reconcile
- the notification policy and optimistic concurrency version

`AgentOperationNotificationPolicy` controls which transitions can become semantic model input:

```csharp
new AgentOperationNotificationPolicy
{
    IncludeInputRequired = true,
    IncludeProgress = false,
    IncludeTerminal = true,
    MinimumInterval = TimeSpan.FromSeconds(5)
}
```

Terminal notifications can wake a started runtime and queue the operation result as hidden model
context. Progress notifications are opt-in because they can be frequent and noisy. A source-specific
operation may still expose richer controls through its own tools or handle ID; HPD does not have a
generic public `BackgroundHandle` API.

## Events

| Event | Meaning |
| --- | --- |
| `AgentOperationRegisteredEvent` | The complete initial operation snapshot was committed. |
| `AgentOperationTransitionedEvent` | A version-checked status or observation transition was committed. |
| `AgentOperationNotificationQueuedEvent` | A terminal or selected transition was queued for model delivery. |
| `AgentOperationNotificationDeliveredEvent` | Queued operation context reached a model turn. |
| `AgentOperationNotificationSuppressedEvent` | Policy or integrity checks suppressed model delivery. |
| `AgentOperationTombstonedEvent` | Retention compacted a terminal operation snapshot into a lightweight tombstone. |

Use these events to build operation timelines. Hosted clients can also read the authoritative
operation projection from the active thread execution returned by runtime state and execution
routes.

The terminal-operation tombstone removes the full snapshot from active operation projections while
retaining the operation identity and provider deduplication keys. That retained state prevents a
late transition or duplicate provider delivery from recreating an operation that already reached a
terminal state.

## Boundaries

Do not confuse operation registration with provider durability. HPD durably records the facts it
knows in the thread journal, while the provider or source system remains authoritative for work it
owns. Recovery references are protected metadata, not credentials.

Use:

- provider background responses when the model provider owns the long-running response
- background invocation when HPD should launch and observe a function, subagent, or workflow
- `StartOperationAsync(...)` when a tool owns a custom long-running resource
- an application-owned queue or durable workflow when work must survive more than the operation
  contracts of the selected runtime and provider

## Related Pages

- [Events Reference](../reference/events.md)
- [Tool And Function Events](../guides/events/tool-and-function-events.md)
- [Tools, Functions, And Harnesses](tools-functions-and-harnesses.md)
- [Subagents](../guides/agents/subagents.md)
- [Hosted Lifecycle And Recovery](../guides/hosting/hosted-lifecycle-and-recovery.md)
