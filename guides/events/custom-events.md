# Custom Events

Custom events let your app put product-specific progress, audit, and UI signals into the same stream as text, tool calls, permissions, workflows, and diagnostics.

Use them when a host, TUI, dashboard, workflow view, or integration needs to observe something that is not a built-in HPD event.

## Define The Event

Create a concrete, non-generic record that inherits from `AgentEvent`:

```csharp
using HPD.Agent;

public sealed record RetrievalProgressEvent(
    string Query,
    int DocumentsScanned,
    int DocumentsMatched) : AgentEvent;
```

The event type name becomes the wire discriminator. `RetrievalProgressEvent` becomes `RETRIEVAL_PROGRESS`.

Use `[EventType(...)]` when you need a stable custom discriminator or need to resolve a naming collision:

```csharp
using HPD.Agent;
using HPD.Agent.Serialization;

[EventType("RETRIEVAL_PROGRESS_V2")]
public sealed record RetrievalProgressEvent(
    string Query,
    int DocumentsScanned,
    int DocumentsMatched) : AgentEvent;
```

Keep discriminator names in `SCREAMING_SNAKE_CASE`.

## Publish It

Middleware and hook contexts publish custom events through the same surface as built-in events:

```csharp
public async Task BeforeFunctionAsync(
    BeforeFunctionContext context,
    CancellationToken cancellationToken)
{
    if (context.Function?.Name == "search_documents")
    {
        await context.PublishAsync(new RetrievalProgressEvent(
            Query: "user query",
            DocumentsScanned: 0,
            DocumentsMatched: 0), cancellationToken);
    }

    await Task.CompletedTask;
}
```

Events published from middleware enter the current runtime context. Durable events use thread-scoped commit-before-publish semantics; live-only events route through the runtime coordinator and retain `ThreadSequenceNumber == 0`. Subscriber handlers may process from their mailboxes asynchronously.

## Publish From A Tool

Tool functions can publish custom events too. Add `FunctionExecutionContext` as a runtime-only parameter on an `[AIFunction]` method:

```csharp
using HPD.Agent;
using HPD.Agent.Middleware;

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

        var scanned = 0;
        var matched = 0;

        await foreach (var document in SearchIndexAsync(query, cancellationToken))
        {
            scanned++;

            if (document.IsMatch)
                matched++;

            await context.PublishAsync(
                new RetrievalProgressEvent(query, scanned, matched),
                cancellationToken);
        }

        return $"Matched {matched} documents.";
    }
}
```

`FunctionExecutionContext` is not shown to the model as a tool argument. The source generator treats it as a runtime parameter, excludes it from the generated tool schema and DTO, and supplies it when the agent invokes the function. Use it when the tool itself knows meaningful progress that middleware cannot infer from the outside.

The context also carries the current function call id, function name, run config, result metadata, services, content store, operation registry, and event coordinator. `PublishAsync(...)` stamps invocation scope and trace information, commits thread-scoped events, and returns the event value that was published.

## Subscribe To It

Subscribe before the run starts:

```csharp
using var retrievalProgress = agent.Subscribe<RetrievalProgressEvent>(evt =>
{
    ui.UpdateRetrieval(evt.Query, evt.DocumentsScanned, evt.DocumentsMatched);
});

await agent.RunAsync("Find the latest support article for this issue.");
```

Use `SubscribeAny(...)` when direct in-process code is building a generic event router, logger, hosted stream implementation, or trace view:

```csharp
var codec = agent.Config!.EventComposition!.Codec;

using var allEvents = agent.SubscribeAny(evt =>
{
    var json = codec.Serialize(evt);
    stream.Write(json);
});
```

Workflow subscriptions can receive custom events too when child agent or middleware events bubble into the workflow coordinator:

```csharp
using var progress = workflow.Subscribe<RetrievalProgressEvent>(evt =>
{
    ui.UpdateWorkflowNode(evt.Query, evt.DocumentsScanned);
});
```

## Serialization

Custom events use the same event envelope as built-in events:

```json
{
  "version": "1.0",
  "type": "RETRIEVAL_PROGRESS",
  "query": "refund policy",
  "documentsScanned": 12,
  "documentsMatched": 3
}
```

The concrete event record is the registration. Source generation discovers it
and closes it into the application event composition or the library event
module:

- assembly-local discriminator constants;
- required source-generated JSON metadata;
- durability and owning module metadata;
- the complete generated application or module composition.

Custom events are live-only by default. Add `[DurableEvent]` only when the event
is required to reconstruct thread state, continue or resolve execution, enforce
idempotency, or prove a canonical fact:

```csharp
using HPD.Agent;
using HPD.Agent.Serialization;

[DurableEvent]
public sealed record RetrievalCompletedEvent(
    string Query,
    int DocumentsMatched) : AgentEvent;
```

Serialize through the codec selected for the agent:

```csharp
var codec = agent.Config!.EventComposition!.Codec;
var json = codec.Serialize(new RetrievalProgressEvent("refund policy", 12, 3));
```

There is no runtime `RegisterEventType` path for `AgentEvent` values. The event
must be present in the selected application composition before the agent is
built.

For Native AOT, provide the source-generated JSON metadata required by the
event contract. Do not replace composition with reflection or a process-global
registry.

## Generator Rules

The source generator accepts concrete, non-generic records that inherit from `AgentEvent`.

It reports:

| Diagnostic | Meaning |
| --- | --- |
| `HPDAEVT005` | An inferred or explicit discriminator is invalid or duplicated in its serializer surface. Rename one event or add a distinct `[EventType("...")]`. |
| `HPD011` | A custom event is generic. Create a concrete event type instead. |
| `HPD012` | An abstract event inherits from `AgentEvent`; it is valid as a base type but is not registered. |

Prefer top-level public event records for events that need to cross assembly, hosted, or serialized boundaries.

## Composition Requirements

The event must be part of the active application composition:

- In an executable, the generated application composition includes the event.
- In a library, the generated module must be referenced and closed into the
  consuming application composition.
- In a multi-application process, select the intended composition with
  `WithEventApplicationIdentity(...)`.
- In a custom host, select the known composition with
  `WithEventComposition(...)` and use its codec for persistence.

An event that is merely declared in a loaded assembly is not automatically
admitted to every application composition.

## Canonical Publication

Publishing a `[DurableEvent]` custom event from a session-and-thread-scoped
runtime context commits it to the canonical thread journal before subscribers
receive it. Replay, reconnect catch-up, simultaneous observers, and live
subscribers therefore receive the same composed event type, payload, `EventId`,
and `ThreadSequenceNumber`.

Live-only custom events remain process-local and use `ThreadSequenceNumber == 0`,
even when published from a scoped context. Use `[DurableEvent]` only for activity
that belongs in thread reconstruction and replay.

Consumers should use one projection handler for both historical and live delivery. The delivery mode may change batching or animation cadence, but it does not change the event payload.

## Related Pages

- [Events Overview](overview.md)
- [Application Composition](application-composition.md)
- [Canonical And Process-Local Events](canonical-and-process-local-events.md)
- [Event Streams And Hierarchies](../../concepts/event-streams-and-hierarchies.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
- [Middleware Overview](../middleware/overview.md)
- [Workflow Events](../multi-agent/workflow-events.md)
