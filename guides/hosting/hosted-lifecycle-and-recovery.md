# Hosted Lifecycle And Recovery

Hosted clients use one server-authoritative lifecycle for control state, committed history, live observation, input, responses, and interruption.

The runtime scope is:

```text
agentId + sessionId + threadId
```

## The Lifecycle

Opening or recovering a thread uses two independent reads:

```text
GET /state
  -> restore activeRun and pendingRequests immediately
  -> capture observedHead = N

GET /events?after={appliedCursor}
  -> catch up canonical events after the client's cursor
  -> remain attached for future commits
  -> advance appliedCursor only after projection succeeds
```

Submitting work remains a separate command:

```text
POST /inputs
  -> 202 { runtimeRunId, startedAt }
```

The backend owns execution. Disconnecting an observer does not cancel the run.

## Read Control State

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state
```

Example:

```json
{
  "observedHead": 42,
  "activeRun": {
    "runtimeRunId": "run-123",
    "agentId": "assistant",
    "sessionId": "session-1",
    "threadId": "main",
    "status": "active"
  },
  "pendingRequests": []
}
```

`/state` is deliberately metadata-only. It does not return event history.

- `observedHead` is the coherent journal boundary observed with the control state.
- `activeRun` restores running and cancellation controls before history finishes loading.
- `pendingRequests` restores permission, clarification, continuation, and client-tool interactions.

`observedHead` is not an applied cursor. A client must not skip to it without projecting the intervening events.

## Catch Up And Remain Live

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after=17
Accept: text/event-stream
```

The stream sends committed catch-up events and then waits for new commits on the same connection.

```text
id: 18
event: agent-event
data: {"version":"1.0","type":"TEXT_DELTA","threadSequenceNumber":18,"text":"hello"}

```

The server also accepts `Last-Event-ID` when the `after` query parameter is absent. It sends comment heartbeats while idle:

```text
: heartbeat

```

Clients must maintain separate notions of received and applied progress. Reconnect only after the greatest contiguous `threadSequenceNumber` whose handlers completed successfully.

1. Decode events in order.
2. Verify that the SSE id equals `threadSequenceNumber`.
3. Apply the event through the same handler used for live delivery.
4. Advance the applied cursor only after successful application.
5. On EOF or transport failure, reconnect from the applied cursor.

If projection fails, stop applying later events. Reconnection redelivers the failing canonical fact.

## Submit And Track A Run

Accepted input returns:

```json
{
  "runtimeRunId": "run-123",
  "startedAt": "2026-07-15T10:15:30Z"
}
```

Only one run can own a thread at a time. Hosting reserves ownership, commits `THREAD_RUN_STARTED`, exposes the active run, executes the input, commits `THREAD_RUN_COMPLETED`, and only then clears active ownership. A conflicting submission returns `409`.

## Interrupt Safely

Read `/state`, then submit the active run id:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/interrupt
Content-Type: application/json

{
  "expectedRuntimeRunId": "run-123",
  "reason": "User cancelled the request."
}
```

Possible statuses are `accepted`, `already_terminal`, `no_active_run`, and `active_run_mismatch`. Never automatically retry against the mismatched run; it may be newer work started by another client.

## Interactive Responses

Pending request events are canonical journal facts and are also included in `/state` while answerable. Send the matching response envelope through:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
```

The request must already be answerable before its committed notification can reach observers. Reopening a client can therefore restore pending interaction UI without replaying the whole thread first.

## Long Disconnects

For a laptop sleep, proxy timeout, or UI closed for hours:

1. Preserve the last successfully applied cursor when possible.
2. Read `/state` to restore control state and capture the current boundary.
3. Open `/events?after={appliedCursor}`.
4. Apply catch-up events through the same projector.
5. Continue on the same stream as commits become live.

A client with no local projection starts at `after=0` and reconstructs the complete conversation incrementally. It retains semantic UI state, not the raw event graph.

## Transport Responsibilities

HTTP owns commands and control state. SSE owns ordered committed observation. Each transport therefore has one lifecycle responsibility.

## Related Pages

- [Canonical Thread Journal](../sessions-and-streaming/canonical-thread-journal.md)
- [Hosted Streaming API](hosted-streaming-api.md)
- [Hosted Endpoints](../../reference/hosted-endpoints.md)
- [TypeScript Client Events](../events/typescript-client.md)
- [Hosted TUI Runtime](../tui/hosted-runtime.md)
