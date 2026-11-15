# TypeScript Client Events

Use the TypeScript client when a browser, Node app, editor extension, or custom UI needs to render HPD Agent runs.

The TypeScript client opens sessions and threads, observes canonical hosted events, submits inputs, and answers interactive requests.

## Install The Client Surface

The main entry point is `AgentClient`:

```typescript
import { AgentClient, type EventHandlers } from '@hpd-research/hpd-agent-client';

const client = new AgentClient({
  baseUrl: 'http://localhost:5135',
});
```

The client uses HTTP for commands and control state, plus resumable SSE for canonical thread observation.

## Open a session and observe its thread

```typescript
const { session, thread } = await client.sessions.create({
  agentId: 'assistant',
  metadata: { title: 'Support case' },
});

const observation = await thread.observe({
  handlers: {
    TEXT_DELTA: ({ event, route }) => {
      transcript.append(route.origin, event.messageId, event.text);
    },
  },
  onRebase: () => transcript.clear(),
});

try {
  const submission = await thread.send('Summarize this thread.');
  if (submission.status === 'queued') {
    console.log(await submission.execution.wait());
  }
} finally {
  await observation.close();
}
```

Session creation returns the initial `main` thread. Reopen a saved thread with
`(await client.sessions.get(sessionId)).threads.get(threadId)`. When a caller
supplies no agent override, the thread's default agent receives the input.

Observation resolves when the stream opens. Pass an `AbortSignal` to stop
observation when the UI disconnects, and monitor `observation.done` for terminal
stream or handler failures.

## Message Policy And Transcript Visibility

Message roles are provider-facing model roles. They are not enough to decide whether something belongs in a human transcript.

Message and text-start events can include HPD-owned policy fields:

```typescript
const handlers = {
  TEXT_MESSAGE_START: ({ event }) => {
    // Hosted message policy values follow the C# enums.
    if (event.visibility === 1) {
      transcript.hide(event.messageId);
    } else if (event.visibility === 2) {
      transcript.startDiagnosticMessage(event.messageId, event.source);
    } else if (event.source === 1) {
      transcript.startUserMessage(event.messageId);
    } else if (event.source === 2) {
      transcript.startAssistantMessage(event.messageId);
    }
  },
} satisfies EventHandlers;
```

Use `source`, `visibility`, and `persistence` for HPD behavior:

| Field | Meaning |
| --- | --- |
| `role` | Provider/model role such as `user`, `assistant`, `system`, or `tool` |
| `source` | HPD reason the message exists, such as `UserInput`, `AssistantOutput`, `BackgroundNotification`, or `Steering` |
| `visibility` | Transcript policy: `Transcript`, `Hidden`, or `Diagnostic` |
| `persistence` | Durable message policy when present on message-start events |

For example, background task notifications are sent to the model as `role: "system"` context, but use `source: "BackgroundNotification"` and `visibility: "Hidden"`. A chat UI should not render their XML payload as assistant text.

## Agent Operation Projections

Long-running work from providers, tools, subagents, workflows, MCP servers, and connected clients
uses the unified `AgentOperation` events:

- `AGENT_OPERATION_REGISTERED`
- `AGENT_OPERATION_TRANSITIONED`
- `AGENT_OPERATION_NOTIFICATION_QUEUED`
- `AGENT_OPERATION_NOTIFICATION_DELIVERED`
- `AGENT_OPERATION_NOTIFICATION_SUPPRESSED`
- `AGENT_OPERATION_TOMBSTONED`

Registered and transitioned events carry an `operation` snapshot with `operationId`, source kind,
name, provider status, observation status, control capabilities, timestamps, completion or failure,
and metadata. Group all operation activity by `operationId`.

Notification queued events carry the bounded model notification payload. Delivered and suppressed
events expose the notification ID or suppression reason, respectively. A delivered operation
notification uses hidden `BackgroundNotification` model context and must not become a visible
assistant transcript row.

Runtime state also exposes operations on the active thread execution:

```typescript
const state = await chat.getState();

for (const operation of state?.activeExecution?.operations ?? []) {
  operationsPanel.upsert({
    id: operation.operationId,
    name: operation.name,
    status: operation.providerStatus,
    observation: operation.observationStatus,
    controlHandle: operation.controlHandleId,
    summary: operation.completionSummary,
    failure: operation.failureMessage,
  });
}
```

Execution projections are useful for active-run status, cancellation capabilities, completion
summaries, and artifacts. Use the operation events when the client needs a durable timeline.

## Typed handlers and routed events

Pass keyed `handlers` to `thread.observe()` to infer each event payload.
Use `onEvent` to process all known event types in one callback. The keyed
handler runs before `onEvent`. The SDK awaits both before acknowledging a
committed cursor.

For delegated work, set `hierarchy: 'threadAndDescendants'`. Use `route.origin`
to identify which thread emitted each delivery. Correlate parent-thread
delegation events by `invocationId` and use the child agent, session, and thread
IDs from `SUBAGENT_INVOCATION_STARTED` when opening the child.
Use `onUnknownEvent` for application events that the SDK does not recognize.
The callback receives the raw event and its route.

## Respond to interactive requests

Use `thread.state()` to restore actionable pending requests. Historical requests
may appear in replay after they have been answered. Send a reply through the
thread and agent scope that owns the request.

```typescript
await thread.respond({
  type: 'PERMISSION_RESPONSE',
  permissionId: request.permissionId,
  sourceName: request.sourceName,
  choiceId: choice.id,
});
```

Select `choice.id` from the choices in the pending request. Inspect the response
to determine whether the server accepted it.

For client tools, provide a registered `ClientToolRegistry` in the
`clientTools` observation option. Matching live invocations and replayed invocations that are still pending
trigger the registered handler and a response to the owning runtime. Send tool
harness definitions in `runConfig.tools.clientInput` when submitting a message.

## Recover a thread and control an execution

Use `thread.observe({ after: appliedCursor })` to resume a saved projection.
When the app has no projection, omit `after` to replay from sequence zero in
the current journal generation. Keep `state.observedCursor` distinct from the
projection's applied cursor. The SDK reconnects after the last applied event and
suppresses duplicates.

Hard compaction replaces the journal. Supply `onRebase` to clear projection
state before the SDK resumes from zero in the replacement generation.

An admitted `send()` returns an execution handle. Call `execution.steer()` or
`execution.cancel()` to target that exact execution. Closing observation and
aborting a wait leave the server execution running.

## What Does Not Reach TypeScript

`AgentStructEvent` values do not flow through the TypeScript client. Struct
events are process-local samples on the .NET `StructEventHub`; they are not
`AgentEvent` values, are not encoded by the application composition's
`AgentEventCodec`, and are not sent over hosted SSE.

`AgentStructEventSerializer` can serialize selected struct events for explicit export or diagnostics, but that is not the hosted agent event stream.

If a process-local sample needs to appear in a hosted UI, summarize or convert it into an intentional `AgentEvent`.

## Lifecycle Transport

The live observer connects to:

```text
/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after={generation}:{sequenceNumber}
```

Inputs are posted separately. Response events are posted to the hosted `/responses` route for the current agent, session, and thread.

## Related Pages

- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
- [Bidirectional Events](bidirectional-events.md)
- [Externally Executed Client Tools](../tools/externally-executed-client-tools.md)
- [Canonical And Process-Local Events](canonical-and-process-local-events.md)
- [Serialization And Registration](serialization-and-registration.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
- [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md)
