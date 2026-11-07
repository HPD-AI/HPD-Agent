# Render An Event Stream

Use this guide when you are building a UI, TUI, bot adapter, dashboard, or hosted client that needs to turn HPD events into something people can read.

HPD sends events linearly. Your app chooses a projection:

- transcript: user messages, assistant text, reasoning, and final output
- timeline: model calls, tools, retries, and completion
- interaction queue: permission, continuation, clarification, and client-tool requests
- hierarchy: workflows, subagents, tool calls, and nested agent activity
- debug log: raw event arrival order

## Subscribe Locally

For local agents, subscribe before `RunAsync(...)`:

```csharp
using var events = agent.SubscribeAny(evt =>
{
    Render(evt);
});

var result = await agent.RunAsync("Review this answer.");
```

Use typed subscriptions when one event family is all you need:

```csharp
using var text = agent.Subscribe<TextDeltaEvent>(evt =>
{
    transcript.Append(evt.MessageId, evt.Text);
});
```

## Route Events By Family

Start with a small routing table. Add product-specific rendering after the basics work.

| Event family | Render as |
| --- | --- |
| `TextMessageStartEvent`, `TextDeltaEvent`, `TextMessageEndEvent` | Assistant message block |
| `ReasoningMessageStartEvent`, `ReasoningDeltaEvent`, `ReasoningMessageEndEvent` | Collapsible reasoning block |
| `ToolCallStartEvent`, `ToolCallArgsEvent`, `ToolCallResultEvent`, `ToolCallEndEvent` | Tool activity under the current message or agent |
| `PermissionRequestEvent`, `PermissionResponseEvent`, `PermissionResolvedEvent` | Blocking prompt and terminal decision attached to a tool/function |
| `Workflow*` | Workflow timeline, layer, node, or route |
| Retry, middleware, schema, and diagnostic events | Debug or observability lane |
| Thread events | History and replay projection |

## Build A Minimal Projection

One simple projection model is a node with a stable id, a kind, a label, and the events attached to it:

```csharp
public sealed record EventNode(
    string Id,
    string Kind,
    string Label,
    int Depth,
    List<AgentEvent> Events);
```

Choose the parent key from the most specific field available:

```csharp
static string ProjectionKey(AgentEvent evt) =>
    evt switch
    {
        PermissionRequestEvent e => $"permission:{e.PermissionId}",
        PermissionResponseEvent e => $"permission:{e.PermissionId}",
        PermissionResolvedEvent e => $"permission:{e.PermissionId}",
        ToolCallStartEvent e => $"tool:{e.CallId}",
        ToolCallArgsEvent e => $"tool:{e.CallId}",
        ToolCallResultEvent e => $"tool:{e.CallId}",
        ToolCallEndEvent e => $"tool:{e.CallId}",
        TextDeltaEvent e => $"message:{e.MessageId}",
        WorkflowAgentStartedEvent e => $"workflow:{e.WorkflowName}:agent:{e.AgentId}",
        AgentEvent e when e.Metadata is not null =>
            $"agent:{string.Join("/", e.Metadata.AgentChain)}",
        _ => evt.TraceId ?? evt.EventFlowId ?? "run"
    };
```

This is intentionally a starting point. A production UI may use separate indexes for messages, tools, workflow nodes, and interactive requests.

## Attach Child Events

When you render a hierarchy, prefer this order:

```text
session + thread
  event flow or trace
    agent/workflow
      message
        tool call
          interactive request
```

Examples:

- Attach text deltas to `message:{MessageId}`.
- Attach tool args, result, and end to `tool:{CallId}`.
- Attach permission request/response to `permission:{PermissionId}`, and show it near the matching `CallId`.
- Attach workflow node events to `workflow:{WorkflowName}:agent:{AgentId}`.
- Use `Metadata.AgentChain` to label child agent output when it is present.
- Use `TraceId`, `SpanId`, and `ParentSpanId` for trace views, but do not require every event to have span data.

## Hosted Streams

Hosted SSE sends committed event envelopes in sequence order as described in [Hosted Streaming API](../hosting/hosted-streaming-api.md). The projection rules are the same once the client has parsed each event.

Use HTTP command routes to submit input or respond to bidirectional events such as permissions. Keep commands separate from observation so reconnecting the SSE stream cannot duplicate them.

Do not mix the transport APIs: direct in-process code subscribes with `agent.Subscribe...` and calls `RunAsync(...)`; ASP.NET Core hosted clients restore controls from `/state`, observe SSE after their applied cursor, and submit input to hosted routes.

## Canonical Boundaries

Thread-scoped projectable events commit before publication. Replay and live delivery use the same event types and handlers. Process-local diagnostics and struct events are separate surfaces rather than events that silently disappear during recovery.

For full replay, consume bounded canonical batches from position zero and retain semantic projection state rather than raw event objects. For live UI, continue from the last successfully applied thread position.

## Compacted Thread Views

Render durable thread projection, not raw event count.

When thread history contains `THREAD_HISTORY_COMPACTION_CHECKPOINT`, apply its semantic result. Soft checkpoints coexist with source events, so a transcript may retain, collapse, or hide the compacted range. A hard checkpoint arrives only in a replacement journal generation: discard projection state from the previous generation and rebuild from the replacement stream. An event-log or audit view can show the checkpoint, compacted ids, summary text, and timestamp.

Live `CompactionEvent` belongs in a diagnostic lane. It is not the durable thread-history projection instruction.

## Related Pages

- [Event Streams And Hierarchies](../../concepts/event-streams-and-hierarchies.md)
- [Tool And Function Events](../events/tool-and-function-events.md)
- [Bidirectional Events](../events/bidirectional-events.md)
- [Lifecycle, Retry, And Error Events](../events/lifecycle-retry-and-error-events.md)
- [Thread History And Forking](thread-history-and-forking.md)
- [Compaction](compaction.md)
- [Workflow Events](../multi-agent/workflow-events.md)
- [Events Reference](../../reference/events.md)
