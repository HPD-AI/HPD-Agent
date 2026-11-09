# Testing Event-Driven Code

Test event-driven code by subscribing before the run, capturing the events you care about, and asserting on event types and correlation ids.

## Capture Events

```csharp
var events = new List<AgentEvent>();

using var subscription = agent.SubscribeAny(evt =>
{
    events.Add(evt);
});

await agent.RunAsync("Summarize this thread.");

Assert.Contains(events, evt => evt is MessageTurnStartedEvent);
Assert.Contains(events, evt => evt is MessageTurnFinishedEvent);
```

Subscribe before `RunAsync(...)`. Disposing the subscription stops capture.

## Assert Tool Flow

```csharp
var toolEvents = events
    .OfType<ToolCallStartEvent>()
    .ToList();

var search = Assert.Single(toolEvents, evt => evt.Name == "search_documents");

Assert.Contains(events.OfType<ToolCallEndEvent>(), evt => evt.CallId == search.CallId);
```

Use `CallId` to connect tool start, args, result, and end events.

## Assert Custom Events

```csharp
var progress = events.OfType<RetrievalProgressEvent>().ToList();

Assert.NotEmpty(progress);
Assert.All(progress, evt => Assert.Equal("refund policy", evt.Query));
```

For custom events that cross a hosted or persisted boundary, test the codec from
the same application composition selected by the agent:

```csharp
var codec = agent.Config!.EventComposition!.Codec;
var json = codec.Serialize(new RetrievalProgressEvent("refund policy", 12, 3));

var roundTripped = Assert.IsType<RetrievalProgressEvent>(
    codec.DeserializeEvent(json));

Assert.Equal(3, roundTripped.DocumentsMatched);
```

Use a separate `AgentInputCodec` when testing semantic input envelopes. Do not
use the output-event codec for `AgentInputEvent`.

## Assert Bidirectional Flows

For request/response flows, subscribe to the request event and respond inside the handler:

```csharp
using var permission = agent.Subscribe<PermissionRequestEvent>(request =>
{
    var allow = request.Choices.Single(choice => choice.Id == "allow_once");
    return agent.AnswerRequestAsync(new PermissionResponseEvent(
        PermissionId: request.PermissionId,
        SourceName: request.SourceName,
        ChoiceId: allow.Id));
});

await agent.RunAsync("Use the file tool if needed.");
```

Then assert the resulting observability events or final output.

## Keep Tests Stable

Prefer assertions on:

- event type
- `CallId`
- request id such as `PermissionId`
- `MessageId`
- custom event payload fields

Avoid relying on exact global ordering across unrelated event families unless the behavior under test requires it. Model output, tool calls, retries, and diagnostics may interleave differently across providers or middleware.

## Related Pages

- [Streaming Events](../../getting-started/streaming-events.md)
- [Tool And Function Events](tool-and-function-events.md)
- [Bidirectional Events](bidirectional-events.md)
- [Custom Events](custom-events.md)
- [Application Composition](application-composition.md)
