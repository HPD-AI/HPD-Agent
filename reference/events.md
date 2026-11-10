# Events

Events are the common vocabulary for HPD Agent runtime activity. Local subscriptions, committed hosted SSE, TUI runtimes, bot adapters, middleware, and thread history all use related event types.

This page is family-based. It is not an exhaustive generated schema reference.

## Event Envelope

Hosted SSE uses envelopes produced by the selected application composition's
`AgentEventCodec`:

```json
{
  "version": "1.0",
  "type": "TEXT_DELTA",
  "threadSequenceNumber": 43,
  "text": "hello",
  "messageId": "message-id"
}
```

The envelope includes injected `version` and `type` fields. Built-in type names use `SCREAMING_SNAKE_CASE`. Payload properties are camelCase and null values are omitted.

Unknown or missing `type` values cannot be deserialized as known events. HTTP input treats invalid event envelopes as bad requests.

The envelope version is strict. HPD Agent `0.9.5` accepts only `"version":
"1.0"` and rejects missing or unsupported versions.

The event catalog exposes a structural contract digest per discriminator. It
covers the serialized property shape independently of effective durability.

Custom event types must be part of the selected generated application
composition and must provide AOT-safe JSON metadata. See
[Application Composition](../guides/events/application-composition.md) and
[Custom Events](../guides/events/custom-events.md).

For the practical serialization path, see [Serialization And Registration](../guides/events/serialization-and-registration.md).

## Durability

`Durable` events are canonical thread facts. They are appended before live
dispatch, survive restart, participate in replay, and contribute to the
composition compatibility digest.

`LiveOnly` events are observable runtime activity. They can be streamed,
archived by a host, or sent to telemetry, but cannot enter a thread journal.
Diagnostics, retries, progress, execution snapshots, skill read/script
mechanics, raw content I/O, and explicit runtime-extension live events use this
classification.

The distinction is part of the application contract. A sufficiently important
live fact needs its own durable event rather than a blanket promotion of a
diagnostic event.

Hosts can select overridable event durability through
`AgentBuilder.WithEventDurability<TEvent>(...)`,
`WithEventDurability(discriminator, ...)`, or
`WithEventDurabilityPolicy(...)`. This choice is frozen at agent construction.
Recovery-critical events marked as required-durable reject a live-only
override.

Text and reasoning delta journaling is coalesced above the session-store
abstraction. Custom `ISessionStore` implementations receive one compact delta
plus the message boundary rather than one durable record per provider token.

## Channels, Direction, And Canonical Publication

Event channel and direction help route and render events. Canonical thread scope and publication determine durability.

Common channel meanings:

| Channel | Typical use |
| --- | --- |
| Streaming | text and reasoning deltas |
| Interactive | permission, continuation, clarification, and client-tool request/response flows |
| Control | thread-run lifecycle and interruption handling |
| Default or inherited | lifecycle, diagnostic, and framework-specific events that do not explicitly set a channel |

Response events for permission, continuation, and clarification use upstream direction.

A thread-scoped projectable event published through `IAgentEventPublisher` is
committed before publication and is replayable by definition. Process-local
diagnostics, struct events, and stateless output use separate paths.

## Correlation And Projection Fields

Events arrive as a stream. Clients can project that stream into transcripts, timelines, workflow views, prompt queues, or trace trees by using correlation fields when they are present.

Common fields:

| Field | Meaning |
| --- | --- |
| `sessionId`, `threadId` | Durable runtime scope when present |
| `threadSequenceNumber` | Authoritative positive journal position; zero means uncommitted/stateless |
| `eventFlowId` | Flow grouping used by thread history and replay; often a message-turn flow for persisted events |
| `traceId` | Execution trace id for a message turn |
| `spanId`, `parentSpanId` | Span-style hierarchy when present |
| `metadata` | Agent attribution, including agent name, id, parent id, chain, and depth |
| `messageId` | Text, reasoning, and tool activity under a message |
| `source` | HPD message source for message/text events, such as `UserInput`, `AssistantOutput`, `BackgroundNotification`, or `Steering` |
| `visibility` | HPD transcript visibility for message/text events: `Transcript`, `Hidden`, or `Diagnostic` |
| `persistence` | HPD message persistence policy when present on durable message-start events |
| `callId` | Tool-call lifecycle grouping |
| `invocationId` | One durable parent-to-subagent delegation; not the child agent or thread identity |
| `permissionId`, `continuationId`, `requestId` | Request/response grouping through `IRequestCorrelatedEvent.RequestId` |

Do not require every event to have every field. For example, some tool events carry `traceId` and `callId` without explicit span fields. Use the most specific available id for the projection you are building. See [Event Streams And Hierarchies](../concepts/event-streams-and-hierarchies.md) and [Render An Event Stream](../guides/sessions-and-streaming/render-an-event-stream.md).

## Canonical Event Parity

Hosted live delivery and durable replay decode to the same event value from the
selected composition. SSE adds `id`, `event`, and heartbeat framing; it does not
define a second event schema. Stable agent attribution required for projection
must be canonical data. Volatile delivery information belongs outside the event
payload.

## Event Families

| Family | Examples | Primary channel | Thread persistence |
| --- | --- | --- | --- |
| Input | `UserMessagesInputEvent`; hosted routes also accept command bodies and interruption requests | upstream/control | command plane |
| Thread execution | `ThreadExecutionStartedEvent`, `ThreadExecutionFinishedEvent` | control | canonical in thread scope |
| Subagent delegation | `SubAgentInvocationStartedEvent`, `SubAgentInvocationCompletedEvent`, `SubAgentInvocationFailedEvent`, `SubAgentInvocationCancelledEvent` | control | canonical in the parent thread; identifies the durable child scope |
| Message turn lifecycle | `MessageTurnStartedEvent`, `MessageTurnFinishedEvent`, `MessageTurnErrorEvent` | default/control | canonical in thread scope |
| Agent turn lifecycle | `AgentTurnStartedEvent`, `AgentTurnFinishedEvent` | default | started is live-only by default; finished is durable and canonical in thread scope |
| Text streaming | `TextMessageStartEvent`, `TextDeltaEvent`, `TextMessageEndEvent` | streaming | canonical in thread scope |
| Reasoning streaming | `ReasoningMessageStartEvent`, `ReasoningDeltaEvent`, `ReasoningMessageEndEvent` | streaming | canonical in thread scope |
| Tool calls | `ToolCallStartEvent`, `ToolCallArgsEvent`, `ToolCallResultEvent`, `ToolCallEndEvent` | default/streaming | canonical in thread scope |
| Workflow execution | `WorkflowStartedEvent`, `WorkflowAgentStartedEvent`, `WorkflowEdgeTraversedEvent`, `WorkflowCompletedEvent` | default/diagnostic | follows the workflow publication scope |
| Agent operations | `AgentOperationRegisteredEvent`, `AgentOperationTransitionedEvent`, operation notification events | control/default | canonical in thread scope |
| Interactive middleware | request events implementing `IAgentRequestEvent<TResponse>` and response events implementing `IAgentResponseEvent` | interactive | canonical in thread scope; answerable requests also appear in runtime state |
| Retry and error policy | `FunctionRetryEvent`, `ModelCallRetryEvent`, middleware error events | diagnostic/default | follows publication scope |
| Compaction observability | `CompactionEvent` | default/diagnostic | follows publication scope |
| Thread-history compaction | `ThreadHistoryCompactionCheckpointEvent` | thread history | canonical checkpoint; soft appends and hard replaces the journal generation |
| Content and references | upload/reference events | default/diagnostic | event publication and content storage are separate policies |
| Audio transcripts | `UserAudioTranscriptDeltaEvent`, `UserAudioTranscriptCompletedEvent`, `UserAudioTranscriptFailedEvent` | streaming/default | follows publication scope; raw audio uses content/audio storage policy |
| Assistant audio output | assistant audio output, stream, artifact, failure, and playback events | default/diagnostic | follows publication scope |
| Struct events | `AgentStructEvent` samples on `StructEventHub` routes | process-local/export | process-local sample rail |
| Planning, schema, structured output | framework diagnostics and state changes | default/diagnostic | follows publication scope |

`CompactionEvent` reports `Started`, `Skipped`, `Failed`, or `Completed`, plus its automatic, explicit, or fork origin. The successful semantic change is carried by the checkpoint event.

## Agent Operations And Notifications

`AgentOperation` is the unified lifecycle for provider background responses, background tool and
subagent invocations, multi-agent workflows, remote MCP tasks, and client-owned background work.

| Event | Meaning |
| --- | --- |
| `AgentOperationRegisteredEvent` | The complete initial operation snapshot was committed. |
| `AgentOperationTransitionedEvent` | A version-checked provider or observation transition was committed. |
| `AgentOperationNotificationQueuedEvent` | A selected transition was queued as hidden model context. |
| `AgentOperationNotificationDeliveredEvent` | Queued operation context reached a model turn. |
| `AgentOperationNotificationSuppressedEvent` | Policy, deduplication, or integrity checks suppressed delivery. |
| `AgentOperationTombstonedEvent` | Retention compacted a terminal operation. |
| `AgentOperationTombstoneEvictedEvent` | The retained operation tombstone expired. |

Operation snapshots carry the authoritative `OperationId`, optional provider operation ID, source
kind, name, owning agent/session/thread, originating execution, optional function invocation,
provider status, observation status, control surface, completion or failure, protected recovery
reference, version, and metadata.

`AgentOperationNotificationPolicy` controls semantic model delivery:

```json
{
  "includeInputRequired": true,
  "includeProgress": false,
  "includeTerminal": true,
  "deduplicationKey": null,
  "minimumInterval": "00:00:05"
}
```

Terminal notifications are delivered optimally during a matching active turn; otherwise HPD queues
them as subsequent work. Delivered operation context uses `source: "BackgroundNotification"` and
`visibility: "Hidden"` and must not be rendered as an assistant transcript row.

Hosted runtime state exposes operations through `activeExecution.operations`. Execution projections
include operation status, control capabilities, handle ID when present, completion summary,
artifact references, failure, timestamps, and metadata.

For registration, invocation modes, and hosted projections, see
[Agent Operations, Background Work, And Notifications](../concepts/background-tasks-and-notifications.md).

## Struct Events

Struct events are a separate process-local surface for realtime samples. HPD
Agent-owned samples implement `AgentStructEvent`, which is an agent-level marker
over the lower HPD Events struct-event contract. They do not inherit from
`AgentEvent`, do not use the application composition's `AgentEventCodec`, and do
not appear in hosted SSE or thread history unless a component explicitly
converts or summarizes them as an `AgentEvent`.

Selected struct events can be exported with `AgentStructEventSerializer`. That serializer uses a separate envelope from hosted `AgentEvent` streaming and is for diagnostics, local capture, replay tooling, or telemetry export. Serializing a struct event does not make it visible to the TypeScript client or durable thread history.

Use `agent.ObserveStruct<TEvent>(...)` or `agent.StructEvents.Route<TEvent>()` for local observation. Tools that accept `FunctionExecutionContext` can access `context.StructEvents` for the same process-local sample lanes. Use this for high-volume diagnostics such as audio playout samples or queue-depth telemetry, not for semantic workflow facts, permissions, or persisted progress.

## Assistant Audio Output Events

Assistant audio output events use the same envelope as other `AgentEvent` values. The event payload includes `sessionId` when the event has a session scope, plus audio-specific identifiers such as `outputFlowId`, `responseId`, `segmentId`, and provider or playback fields.

Source-generated assistant audio output type names include:

| Type | Purpose |
| --- | --- |
| `ASSISTANT_AUDIO_OUTPUT_STARTED` | Assistant audio output flow started. |
| `ASSISTANT_AUDIO_OUTPUT_STREAM_STARTED` | A synthesized audio stream segment started. |
| `ASSISTANT_AUDIO_OUTPUT_CHUNK_READY` | An audio chunk is ready. |
| `ASSISTANT_AUDIO_PUSH_TEXT_STREAM_OPENING` | Push-text TTS stream is opening. |
| `ASSISTANT_AUDIO_PUSH_TEXT_STREAM_OPENED` | Push-text TTS stream opened. |
| `ASSISTANT_AUDIO_PUSH_TEXT_INPUT_SENT` | Text input was sent to a push-text TTS stream. |
| `ASSISTANT_AUDIO_OUTPUT_STREAM_COMPLETED` | An audio stream segment completed. |
| `ASSISTANT_AUDIO_OUTPUT_ARTIFACT_CAPTURED` | Audio output was captured as an artifact. |
| `ASSISTANT_AUDIO_OUTPUT_SEGMENT_FAILED` | One audio segment failed. |
| `ASSISTANT_AUDIO_OUTPUT_COMPLETED` | Assistant audio output flow completed. |
| `ASSISTANT_AUDIO_OUTPUT_FAILED` | Assistant audio output flow failed. |
| `ASSISTANT_AUDIO_PLAYBACK_QUEUED` | Audio playback was queued. |
| `ASSISTANT_AUDIO_PLAYBACK_STARTED` | Audio playback started. |
| `ASSISTANT_AUDIO_PLAYBACK_PROGRESS` | Audio playback progress was reported. |
| `ASSISTANT_AUDIO_PLAYBACK_COMPLETED` | Audio playback completed. |
| `ASSISTANT_AUDIO_PLAYBACK_INTERRUPTED` | Audio playback was interrupted. |
| `ASSISTANT_AUDIO_PLAYBACK_FAILED` | Audio playback failed. |

Value round-trip tests cover `sessionId`, output flow, response, segment, chunk, artifact, error, and playback fields for these event families.

## UI Rendering Guidance

For text output, render `TextMessageStartEvent`, append `TextDeltaEvent`, and close the block on `TextMessageEndEvent`. Do not infer transcript behavior from `role` alone. `role` is provider-facing model protocol; `visibility` and `source` are HPD-owned rendering policy. A text stream with `visibility: "Hidden"` is model/runtime context and should not become a visible transcript row. `visibility: "Diagnostic"` should render as an internal or diagnostic row when the app chooses to show it.

Common visible transcript cases:

| Source | Visibility | Suggested rendering |
| --- | --- | --- |
| `UserInput` | `Transcript` | user message |
| `AssistantOutput` | `Transcript` | assistant message |
| `BackgroundNotification` | `Hidden` | no transcript row; the model receives this context |
| `RuntimeContext` | `Hidden` | no transcript row |
| `Steering` | `Hidden` or `Diagnostic` | hidden runtime input or explicit diagnostic row |

For reasoning output, use the same start/delta/end pattern but render it separately from final user-visible text.

For tool calls, use start/args/result/end events to show activity, arguments, and results. Tool events may arrive interleaved with streaming text depending on the model and middleware.

For interactive middleware, render request events and answer with the matching `IAgentResponseEvent` through the hosted HTTP `/responses` route or `agent.AnswerRequestAsync(...)` in a local app. Permission, continuation, clarification, and client-tool responses are coordination events, not ordinary output. See [Bidirectional Events](../guides/events/bidirectional-events.md).

Request-specific event families should define only the domain request and domain response events. Do not add family-specific terminal lifecycle events such as `FooApprovedEvent`, `FooDeniedEvent`, `FooResolvedEvent`, or `FooCancelledEvent`; use the generic request lifecycle events for request-session state.

For JavaScript and TypeScript apps, use `AgentClient` to observe canonical thread events from an applied cursor, handle unknown custom events through `onAny(...)`, and send response events back to the hosted runtime. See [TypeScript Client Events](../guides/events/typescript-client.md).

For retry events, be careful with already-rendered partial output. A `ModelCallRetryEvent` can arrive after partial model content, so UIs may need to clear or mark stale partial text before rendering retry output.

For lifecycle, retry, and failure rendering, see [Lifecycle, Retry, And Error Events](../guides/events/lifecycle-retry-and-error-events.md).

Use thread-execution start/finish events to project historical hosted work. Use authoritative `activeExecution` runtime state—not replayed lifecycle events or thread-operation-lock conflicts—to decide whether a thread is currently busy or cancellable.

For subagents, project `SUBAGENT_INVOCATION_STARTED` and its completed, failed, or cancelled terminal event by `invocationId`. The started event provides `childAgentId`, `childSessionId`, and `childThreadId`, which are the continuation route. The invocation id correlates one delegation and is not a replacement for the child `AgentId` or thread id.

For compaction, distinguish lifecycle facts from the projection checkpoint. `CompactionEvent` records start and terminal status. `ThreadHistoryCompactionCheckpointEvent` records the compaction point and semantic result. Soft mode appends it while preserving source events. Hard mode replaces the complete journal with a new generation containing the checkpoint and configured result.

For workflows, render workflow lifecycle, layer, node, edge, and diagnostic events as a timeline or graph projection. Child agent, tool, and permission events may also appear in the same live stream when the workflow or subagent execution bubbles events through a parent coordinator.

For tool progress emitted from inside a function body, see [Tool And Function Events](../guides/events/tool-and-function-events.md).

## Publication Summary

Thread-scoped projectable `AgentEvent` values pass through the canonical publisher:

```text
identify -> commit -> assign ThreadSequenceNumber -> publish -> replay
```

The selected composition's codec preserves the concrete event type and canonical
payload across in-process subscribers, hosted observation, and replay.
Process-local publication keeps `ThreadSequenceNumber == 0` and provides no
replay guarantee. High-volume `AgentStructEvent` values use the separate
struct-event rail.

See [Canonical And Process-Local Events](../guides/events/canonical-and-process-local-events.md) and [Canonical Thread Journal](../guides/sessions-and-streaming/canonical-thread-journal.md).
