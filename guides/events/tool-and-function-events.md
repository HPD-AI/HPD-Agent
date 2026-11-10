# Tool And Function Events

Tool events let a UI show what the model asked a tool to do. Function execution context lets the tool report what it is doing while it runs.

Use both when a tool can take noticeable time, call external systems, process files, or run a workflow-like operation behind one model tool call.

## Subscribe To Tool Activity

Subscribe before the agent run starts:

```csharp
using var toolStart = agent.Subscribe<ToolCallStartEvent>(evt =>
{
    ui.StartTool(evt.CallId, evt.Name);
});

using var toolArgs = agent.Subscribe<ToolCallArgsEvent>(evt =>
{
    ui.ShowToolArguments(evt.CallId, evt.ArgsJson);
});

using var toolResult = agent.Subscribe<ToolCallResultEvent>(evt =>
{
    ui.ShowToolResult(evt.CallId, evt.Result);
});

using var toolEnd = agent.Subscribe<ToolCallEndEvent>(evt =>
{
    ui.EndTool(evt.CallId);
});

await agent.RunAsync("Search the support docs and summarize the answer.");
```

Group tool lifecycle events by `CallId`. If a model emits text while tools are running, text and tool events can be interleaved in the live stream.

`ToolCallStartEvent.Name` is the function/tool name. `ToolCallResultEvent.Result` is a `ToolResultPayload`, which can carry text, JSON, or richer client-tool content.

## Emit Progress From The Tool

When the tool itself knows meaningful progress, accept `FunctionExecutionContext` as a runtime-only parameter and emit events from inside the function body:

```csharp
using HPD.Agent;
using HPD.Agent.Middleware;

public sealed record RetrievalProgressEvent(
    string Query,
    int DocumentsScanned,
    int DocumentsMatched) : AgentEvent;

public sealed class RetrievalTools
{
    [AIFunction(Name = "search_documents")]
    public async Task<string> SearchDocuments(
        string query,
        FunctionExecutionContext context,
        CancellationToken cancellationToken)
    {
        await context.PublishAsync(
            new RetrievalProgressEvent(query, 0, 0),
            cancellationToken);

        var scanned = 12;
        var matched = 3;

        await context.PublishAsync(
            new RetrievalProgressEvent(query, scanned, matched),
            cancellationToken);
        return $"Matched {matched} documents.";
    }
}
```

`FunctionExecutionContext` and `CancellationToken` are supplied by HPD Agent. They can appear in the C# function signature, but they are excluded from the generated tool schema and argument DTO, so the model does not see them and cannot provide values for them. In the example above, the model only sees real tool arguments such as `query`. Unsupported runtime-like parameters such as `HookContext`, `AgentContext`, `AgentLoopState`, `IEventCoordinator`, `IEventFlowRegistry`, or `ToolResultMetadata` produce generator diagnostic `HPD020`; use `FunctionExecutionContext` instead.

Subscribe to the custom progress event like any other event:

```csharp
using var progress = agent.Subscribe<RetrievalProgressEvent>(evt =>
{
    ui.UpdateRetrieval(evt.Query, evt.DocumentsScanned, evt.DocumentsMatched);
});
```

## When To Use Middleware

Use tool code for domain progress the tool already knows:

- records scanned
- files processed
- external jobs started
- retrieval stages
- provider-specific status

Use middleware for cross-cutting behavior around functions:

- permission checks
- retry and timeout policy
- argument redaction
- audit events
- shared logging

Middleware and tool code publish with `context.PublishAsync(...)`. Durable events
commit before subscribers receive them. Live-only events, including ordinary
progress events, remain process-local with `ThreadSequenceNumber == 0`.

`PublishAsync(...)` returns after canonical commit and publication, not after every subscriber mailbox has finished processing the event.

## Runtime Context

`FunctionExecutionContext` exposes a narrow runtime surface:

- `FunctionCallId`, `FunctionName`, and invocation metadata
- `TraceId`, `SessionId`, and `ThreadId` when present
- `RunConfig` and per-call `ResultMetadata`
- `Services`, `ContentStore`, and runtime capabilities
- `PublishAsync(...)`, `TryPublishAsync(...)`, and `RequestAsync(...)`
- `StructEvents` for process-local realtime sample lanes
- `StartOperationAsync(...)` and `CancelOperationAsync(...)` for runtime-owned operations

It does not expose mutable agent state or hook contexts. Use middleware when the behavior needs to mutate middleware state or wrap scheduler-owned phases.

`StructEvents` is separate from `AgentEvent` streaming. Use it for local hot-path samples, frames, or queue-depth style telemetry that should not be serialized, persisted, replayed, or shown to the model as semantic progress.

## Background Work From Tools

Use `StartOperationAsync(...)` when a tool needs to return promptly while keeping runtime-owned work alive after the function body returns. This is useful for follow-up indexing, long-running cleanup, async provider jobs, or post-processing that should stay attached to the tool call.

```csharp
[AIFunction(Name = "start_indexing")]
public async Task<string> StartIndexing(
    string collection,
    FunctionExecutionContext context,
    CancellationToken cancellationToken)
{
    AgentOperationReceipt receipt = await context.StartOperationAsync(
        "IndexDocuments",
        new Dictionary<string, string> { ["collection"] = collection },
        new AgentOperationNotificationPolicy { IncludeTerminal = true },
        async (operationId, runtimeToken) =>
        {
            await indexer.RebuildAsync(collection, runtimeToken);
            return new AgentOperationCompletion(
                Summary: $"Finished indexing {collection}.");
        },
        cancellationToken);

    return $"Started indexing operation {receipt.OperationId}.";
}
```

The operation snapshot records the authoritative `OperationId`, source kind, owning agent/session/thread, original function invocation, provider and observation status, optional control surface, and notification policy.

The runtime emits canonical lifecycle events for registered operations:

- `AgentOperationRegisteredEvent`
- `AgentOperationTransitionedEvent`
- `AgentOperationNotificationQueuedEvent`
- `AgentOperationNotificationDeliveredEvent`
- `AgentOperationNotificationSuppressedEvent`

Group lifecycle events by `OperationId`, and use `Invocation.FunctionCallId` when attaching the operation to the original tool call. `StartOperationAsync(...)` transfers an owned tool harness execution scope and held client resources to the operation, and completion results can wake a started runtime according to `AgentOperationNotificationPolicy`.

Call `context.CancelOperationAsync(operationId, cancellationToken)` when the operation advertises the `Cancel` capability. A source-specific harness may expose additional status, read, stop, artifact, or process operations through its own tools. There is no generic public `BackgroundHandle` API.

For provider-owned background responses, invocation-mode policies, control projections, and notification semantics, see [Agent Operations, Background Work, And Notifications](../../concepts/background-tasks-and-notifications.md).

## Related Pages

- [Custom Events](custom-events.md)
- [Bidirectional Events](bidirectional-events.md)
- [Author A Tool Harness](../tools/author-a-tool-harness.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
