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

Events published from middleware enter the current runtime context. Thread-scoped publication commits before subscriber delivery; process-local publication routes through the runtime coordinator. Subscriber handlers may process from their mailboxes asynchronously.

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

The context also carries the current function call id, function name, run config, result metadata, services, content store, background-task registry, and event coordinator. `PublishAsync(...)` stamps invocation scope and trace information, commits thread-scoped events, and returns the event value that was published.

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
using var allEvents = agent.SubscribeAny(evt =>
{
    var json = AgentEventSerializer.ToJson(evt);
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

For ordinary app projects, define the event record and let source generation handle the rest. The custom event generator discovers concrete `AgentEvent` records outside HPD framework namespaces and generates:

- assembly-local discriminator constants
- module-initializer registration for `AgentEventSerializer`

That means `AgentEventSerializer` can use the right discriminator without manual registration.

For Native AOT or other strict source-generated JSON paths, register JSON metadata manually with `AgentEventSerializer.RegisterEventType(...)` as shown below. System.Text.Json source generation does not consume JSON context attributes emitted by another source generator, so the custom event generator cannot safely create that context for you.

## Generator Rules

The source generator accepts concrete, non-generic records that inherit from `AgentEvent`.

It reports:

| Diagnostic | Meaning |
| --- | --- |
| `HPD010` | Two custom events resolve to the same discriminator. Rename one event or add `[EventType("...")]`. |
| `HPD011` | A custom event is generic. Create a concrete event type instead. |
| `HPD012` | An abstract event inherits from `AgentEvent`; it is valid as a base type but is not registered. |

Prefer top-level public event records for events that need to cross assembly, hosted, or serialized boundaries.

## Manual Registration

Use manual registration only for special package boundaries or libraries where the source generator is not participating.

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using HPD.Agent;
using HPD.Agent.Serialization;

[JsonSerializable(typeof(RetrievalProgressEvent))]
public partial class AppEventJsonContext : JsonSerializerContext
{
}

internal static class AppEventRegistration
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void RegisterEvents()
#pragma warning restore CA2255
    {
        AgentEventSerializer.RegisterEventType(
            typeof(RetrievalProgressEvent),
            "RETRIEVAL_PROGRESS",
            AppEventJsonContext.Default.RetrievalProgressEvent);
    }
}
```

## Canonical Publication

Publishing from a session-and-thread-scoped runtime context commits the custom event to the canonical thread journal before subscribers receive it. Replay, reconnect catch-up, simultaneous observers, and live subscribers therefore receive the same registered event type, payload, `EventId`, and `ThreadSequenceNumber`.

Publishing without thread scope produces process-local output with `ThreadSequenceNumber == 0`. Use that mode for activity that does not belong in thread reconstruction.

Consumers should use one projection handler for both historical and live delivery. The delivery mode may change batching or animation cadence, but it does not change the event payload.

## Related Pages

- [Events Overview](overview.md)
- [Canonical And Process-Local Events](canonical-and-process-local-events.md)
- [Event Streams And Hierarchies](../../concepts/event-streams-and-hierarchies.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
- [Middleware Overview](../middleware/overview.md)
- [Workflow Events](../multi-agent/workflow-events.md)
