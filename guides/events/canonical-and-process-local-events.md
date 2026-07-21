# Canonical And Process-Local Events

HPD Agent separates canonical thread facts from intentionally process-local activity.

## Canonical Thread Events

A projectable event with a session and thread scope is published through the canonical thread publisher:

```text
append -> assign ThreadSequenceNumber -> publish -> replay
```

The live subscriber and later replay reader receive the same concrete registered event type, payload, `EventId`, scope, and thread position. Canonical append failure produces no live-only fact.

Canonical families include text and reasoning streams, tool lifecycle, thread-run lifecycle, background task lifecycle, interactive requests and accepted responses, middleware state commits, and application-defined events published through the thread context.

Custom canonical events use the registered event codec and become durable when published through a thread-scoped runtime context.

## Process-Local Activity

Use a process-local surface when a value is not a replayable thread fact:

- tracing and metrics;
- high-volume struct events;
- MCP transport progress;
- stateless agent output without a configured journal;
- application diagnostics intentionally excluded from thread projection.

Stateless `AgentEvent` output uses the same event types but has `ThreadSequenceNumber == 0`. It is not committed and cannot be resumed.

`AgentStructEvent` values use `StructEventHub` routes for hot-path samples such as audio frames, queue depth, or provider ticks. They are not hosted SSE events unless an application explicitly projects a semantic summary into a canonical `AgentEvent`.

## Delivery Context Is Separate

Historical, catch-up, and live delivery may use different batching or rendering cadence. Delivery mode is infrastructure context, not a mutation of event payloads.

One handler should be able to project the same event during:

- initial replay;
- reconnect catch-up;
- simultaneous multi-client observation;
- future live commits.

## Compaction

`CompactionEvent` describes middleware execution. When it is published in a canonical thread context, it follows the same journal rule as other projectable events.

`ThreadHistoryCompactionCheckpointEvent` is the canonical projection checkpoint. Soft compaction appends it while preserving the source journal. Hard compaction replaces the complete journal generation with the checkpoint and configured semantic result.

## JSON And Codecs

Canonical storage and hosted SSE use the registered `AgentEvent` codec. SSE adds transport framing, but it does not translate the event into a different durable presentation type:

```text
id: 3:43
event: agent-event
data: {"version":"1.0","type":"TEXT_DELTA","threadSequenceNumber":43,"text":"hello"}
```

Unknown projection-critical schemas fail at their position. Consumers must not silently replace them with another event type or advance past them.

## Related Pages

- [Canonical Thread Journal](../sessions-and-streaming/canonical-thread-journal.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
- [Serialization And Registration](serialization-and-registration.md)
- [Events Reference](../../reference/events.md)
