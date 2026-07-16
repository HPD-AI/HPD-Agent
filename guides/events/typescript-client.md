# TypeScript Client Events

Use the TypeScript client when a browser, Node app, editor extension, or custom UI needs to render HPD Agent runs.

The client is not another event system. It is the JavaScript/TypeScript consumption surface for canonical hosted events: open a session and thread, catch up from an applied cursor, remain attached for future commits, send inputs, and answer interactive requests.

## Install The Client Surface

The main entry point is `AgentClient`:

```typescript
import { AgentClient, EventTypes } from '@hpd-research/hpd-agent-client';

const client = new AgentClient({
  baseUrl: 'http://localhost:5135',
});
```

The client uses HTTP for commands and control state, plus resumable SSE for canonical thread observation.

## Open A Chat Scope

Most apps should scope UI work to one agent, session, and thread:

```typescript
const chat = await client.chat.open({
  agentId: 'assistant',
  threadId: 'main',
  session: {
    create: {
      metadata: { title: 'New chat' },
    },
  },
});
```

Then install event handlers, read runtime state, observe from the application's applied cursor, and submit input:

```typescript
client.on(EventTypes.TEXT_DELTA, (event) => {
  transcript.append(event.messageId, event.text);
});

client.onAny((event) => {
  projectEvent(event);
});

const state = await chat.subscribeLive({ afterSequenceNumber: 0 });
restoreControls(state.activeRun);

const submission = await chat.submitMessage({
  contents: [{ $type: 'text', text: 'Summarize this thread.' }],
});

console.log(submission.runtimeRunId, submission.startedAt);
```

`subscribeLive()` loads metadata-only `/state`, opens the unified SSE stream with `afterSequenceNumber`, and returns the control-state snapshot. Events committed before or during connection are delivered through the same stream. Pass zero when the application has no local projection. Subscribe before submitting input when the UI needs to render the turn as it happens.

`state.observedHead` identifies the committed boundary captured with the control state. It is not permission to skip history and must not replace the consumer's applied cursor.

## Message Policy And Transcript Visibility

Message roles are provider-facing model roles. They are not enough to decide whether something belongs in a human transcript.

Message and text-start events can include HPD-owned policy fields:

```typescript
client.on(EventTypes.TEXT_MESSAGE_START, (event) => {
  if (event.visibility === 'Hidden') {
    transcript.hide(event.messageId);
    return;
  }

  if (event.source === 'UserInput') {
    transcript.startUserMessage(event.messageId);
  } else if (event.source === 'AssistantOutput') {
    transcript.startAssistantMessage(event.messageId);
  } else if (event.visibility === 'Diagnostic') {
    transcript.startDiagnosticMessage(event.messageId, event.source);
  }
});
```

Use `source`, `visibility`, and `persistence` for HPD behavior:

| Field | Meaning |
| --- | --- |
| `role` | Provider/model role such as `user`, `assistant`, `system`, or `tool` |
| `source` | HPD reason the message exists, such as `UserInput`, `AssistantOutput`, `BackgroundNotification`, or `Steering` |
| `visibility` | Transcript policy: `Transcript`, `Hidden`, or `Diagnostic` |
| `persistence` | Durable message policy when present on message-start events |

For example, background task notifications are sent to the model as `role: "system"` context, but use `source: "BackgroundNotification"` and `visibility: "Hidden"`. A chat UI should not render their XML payload as assistant text.

The helper `mapThreadMessages(...)` filters hidden messages from the transcript read model. Use `projectThreadEventsToMessages(...)` directly when an app needs the complete projected thread state, including hidden model/runtime context.

## Background Task Notification Rules

Background task lifecycle events carry a `notification` rule. Older clients may remember this as a simple policy string; current events use a rule object so framework code can stay source-neutral while harnesses add richer behavior.

```typescript
type BackgroundTaskNotificationRule =
  | { kind: 'none' }
  | {
      kind: 'on_final_state';
      completed?: boolean;
      faulted?: boolean;
      cancelled?: boolean;
    }
  | {
      kind: 'strategy';
      name: string;
      parameters?: Record<string, string> | null;
      fallback?: BackgroundTaskNotificationRule | null;
    };
```

Use the rule as descriptive metadata for UI/debugging. Do not infer shell, process, or test-runner behavior from it. When a rule queues model input, the runtime emits `BackgroundTaskNotificationQueuedEvent`; when it suppresses input, it emits `BackgroundTaskNotificationSuppressedEvent`.

Thread-run API projections expose a smaller shape:

```typescript
interface ThreadRunBackgroundTaskNotification {
  kind: string;
  strategyName?: string | null;
}
```

That projection is for run lists and status panes. Use live background task events when an app needs the full rule payload.

## Typed Handlers And Projection

Use `client.on(...)` for event families your app knows how to handle directly:

```typescript
client.on(EventTypes.TOOL_CALL_START, (event) => {
  tools.start(event.callId, event.name);
});

client.on(EventTypes.TOOL_CALL_ARGS, (event) => {
  tools.appendArgs(event.callId, event.argsJson);
});

client.on(EventTypes.TOOL_CALL_RESULT, (event) => {
  tools.setResult(event.callId, event.result);
});
```

Use `client.onAny(...)` for stream-wide projection, diagnostics, custom events, and unknown event types:

```typescript
client.onAny((event) => {
  timeline.push({
    type: event.type,
    timestamp: event.timestamp,
    flow: event.eventFlowId,
  });
});
```

Typed handlers run before `onAny` handlers for the same event. Handlers are awaited in order, so keep UI projection work fast and move expensive side effects out of the hot path.

## Respond To Interactive Events

Permission, continuation, and clarification requests are bidirectional events. When a host delivers a request event, the TypeScript app asks the user or host policy and sends the matching response:

```typescript
client.on(EventTypes.PERMISSION_REQUEST, async (request) => {
  const approved = await permissions.confirm({
    title: request.functionName,
    description: request.description,
    arguments: request.arguments,
  });

  await client.run({
    type: EventTypes.PERMISSION_RESPONSE,
    permissionId: request.permissionId,
    sourceName: request.sourceName,
    approved,
    reason: approved ? undefined : 'Denied by user.',
    choice: 'ask',
  });
});
```

The same pattern applies to `CLARIFICATION_REQUEST` and `CONTINUATION_REQUEST`. The client posts response event envelopes to the hosted `/responses` route for the current chat scope. Preserve the request id from the request event so the hosted runtime can match the pending waiter.

Thread-scoped interactive request records are canonical and replayable. The backend also exposes answerable pending requests in runtime state so reconnecting UIs can restore interaction controls before historical catch-up completes.

Client tools are the exception. If you register a tool handler, the client automatically answers `CLIENT_TOOL_INVOKE_REQUEST` with `CLIENT_TOOL_INVOKE_OUTCOME`:

```typescript
client.tools.register('get_active_view', () => ({
  activeView: 'chat',
}));
```

This registers the local handler only. To make externally executed tools visible to the model, pass tool harness definitions through `runConfig.clientToolInput`. See [Externally Executed Client Tools](../tools/externally-executed-client-tools.md).

Use explicit request handlers only when your app needs to render the client-tool request before or after the automatic response path.

## Custom And Unknown Events

The TypeScript client preserves events whose `type` is not modeled by the package version you are using. Handle app-owned events through `onAny`:

```typescript
type RetrievalProgress = {
  type: 'RETRIEVAL_PROGRESS';
  query: string;
  documentsScanned: number;
  documentsMatched: number;
};

client.onAny((event) => {
  if (event.type !== 'RETRIEVAL_PROGRESS') return;

  const progress = event as RetrievalProgress;
  retrievalPanel.update(progress);
});
```

Register custom event serialization on the .NET side so hosted streams can produce the event envelope. Add TypeScript types locally when the event belongs to your app; add them to the SDK only when the event becomes a shared protocol event.

## Control State, Observation, And Recovery

`chat.subscribeLive()` reads control state, then observes canonical events after the caller's applied cursor. Full reconstruction uses the incremental observation stream starting at zero.

The SSE transport awaits handlers in registration order and advances its cursor only after the consumer completes successfully. If the stream ends or fails, it reconnects after the last acknowledged sequence. Duplicate sequence numbers are ignored.

For a lifecycle-aware UI:

1. Register event handlers.
2. Load the application's last applied thread position, or use zero.
3. Call `subscribeLive({ afterSequenceNumber: appliedCursor })`.
4. Use `state.activeRun` to restore running and cancellation controls.
5. Submit input and retain its authoritative `runtimeRunId`.
6. Apply committed events serially and persist the cursor only after successful projection.

To cancel safely, use `chat.cancelActiveTurn()`. It reloads state and sends the current run as `expectedRuntimeRunId`. Inspect the returned `accepted`, `already_terminal`, `no_active_run`, or `active_run_mismatch` status instead of assuming cancellation occurred.

## What Does Not Reach TypeScript

`AgentStructEvent` values do not flow through the TypeScript client. Struct events are process-local samples on the .NET `StructEventHub`; they are not `AgentEvent` values, not serialized by `AgentEventSerializer`, and not sent over hosted SSE.

`AgentStructEventSerializer` can serialize selected struct events for explicit export or diagnostics, but that is not the hosted agent event stream.

If a process-local sample needs to appear in a hosted UI, summarize or convert it into an intentional `AgentEvent`.

## Lifecycle Transport

The live observer connects to:

```text
/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after={lastAppliedThreadSequenceNumber}
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
