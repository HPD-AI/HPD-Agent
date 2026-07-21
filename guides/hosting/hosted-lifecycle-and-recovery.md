# Hosted Lifecycle And Recovery

Hosted clients use one server-authoritative lifecycle for control state, committed history, live observation, semantic input, and responses.

The runtime scope is:

```text
agentId + sessionId + threadId
```

## The Lifecycle

Opening or recovering a thread uses two independent reads:

```text
GET /state
  -> restore activeExecution and pendingRequests immediately
  -> capture observedCursor = Generation:N

GET /events?after={appliedCursor}
  -> catch up canonical events after the client's cursor
  -> remain attached for future commits
  -> advance appliedCursor only after projection succeeds
```

Submitting work remains a separate command:

```text
POST /inputs
  -> 202 { disposition, threadExecutionId, startedAt?, activeExecution? }
```

The backend owns execution. Disconnecting an observer does not cancel the execution.

## Read Control State

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state
```

Example:

```json
{
  "observedCursor": {
    "generation": 3,
    "sequenceNumber": 42
  },
  "activeExecution": {
    "threadExecutionId": "execution-123",
    "agentId": "assistant",
    "sessionId": "session-1",
    "threadId": "main",
    "status": "active"
  },
  "pendingRequests": []
}
```

`/state` is deliberately metadata-only. It does not return event history.

- `observedCursor` is the coherent journal boundary observed with the control state.
- `activeExecution` restores running and cancellation controls before history finishes loading.
- `pendingRequests` restores permission, clarification, continuation, and client-tool interactions.

`observedCursor` is not an applied cursor. A client must not skip to it without projecting the intervening events.

## Catch Up And Remain Live

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after=3:17
Accept: text/event-stream
```

The stream sends committed catch-up events and then waits for new commits on the same connection.

```text
id: 3:18
event: agent-event
data: {"version":"1.0","type":"TEXT_DELTA","threadSequenceNumber":18,"text":"hello"}

```

The server also accepts `Last-Event-ID` when the `after` query parameter is absent. It sends comment heartbeats while idle:

```text
: heartbeat

```

Clients must maintain separate notions of received and applied progress. Reconnect only from the greatest contiguous `ThreadJournalCursor` whose handlers completed successfully.

1. Decode events in order.
2. Parse the SSE id as `generation:sequence` and verify its sequence equals `threadSequenceNumber`.
3. Apply the event through the same handler used for live delivery.
4. Advance the applied cursor only after successful application.
5. On EOF or transport failure, reconnect from the applied cursor.

If projection fails, stop applying later events. Reconnection redelivers the failing canonical fact.

## Hard Journal Replacement

Hard compaction replaces the active journal and increments its generation. An observer attached to the previous generation receives a control frame and the stream ends:

```text
event: thread-journal-rebased
data: {"previousGeneration":3,"currentGeneration":4}
```

The client must not continue its old projection with generation 4 events. Reload `/state`, clear semantic state derived from generation 3, and connect with `after=4:0`. Active-execution and pending-interaction facts are reseeded into the replacement generation, so controls remain recoverable while the transcript rehydrates.

## Submit And Track An Execution

Accepted input returns:

```json
{
  "disposition": "queued",
  "threadExecutionId": "execution-123",
  "startedAt": "2026-07-15T10:15:30Z"
}
```

Only one execution can own a thread at a time. Hosting reserves ownership, commits `THREAD_EXECUTION_STARTED`, exposes `activeExecution`, executes the input, commits `THREAD_EXECUTION_FINISHED`, and only then clears active ownership. A conflicting submission returns `409`.

## Steer Or Interrupt Safely

Read `/state`, then submit the active execution id:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/inputs
Content-Type: application/json

{
  "version": "1.0",
  "type": "INTERRUPTION_REQUEST",
  "threadExecutionId": "execution-123",
  "reason": "User cancelled the request.",
  "source": "User"
}
```

Steering uses the same route with `type: "STEERING_INPUT"`, the same `threadExecutionId`, and a `messages` array. Possible dispositions include `accepted`, `no_active_execution`, `active_execution_mismatch`, `active_input_not_steerable`, and `execution_finishing`. Never automatically retry against a mismatched execution; it may be newer work started by another client.

## Interactive Responses

Pending request events are canonical journal facts and are also included in `/state` while answerable. Send the matching response envelope through:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
```

The request must already be answerable before its committed notification can reach observers. Reopening a client can therefore restore pending interaction UI without replaying the whole thread first.

## Long Disconnects

For a laptop sleep, proxy timeout, or UI closed for hours:

1. Preserve the last successfully applied cursor when possible.
2. Read `/state` to restore control state and capture the current generation boundary.
3. Open `/events?after={generation}:{sequence}`.
4. Apply catch-up events through the same projector.
5. Continue on the same stream as commits become live.

A client with no local projection starts at `after=0` and reconstructs the complete conversation incrementally. It retains semantic UI state, not the raw event graph.

## Transport Responsibilities

HTTP owns commands and control state. SSE owns ordered committed observation. Each transport therefore has one lifecycle responsibility.

## Related Pages

- [Canonical Thread Journal](../sessions-and-streaming/canonical-thread-journal.md)
- [Thread Execution Lifecycle](../sessions-and-streaming/thread-execution-lifecycle.md)
- [Hosted Streaming API](hosted-streaming-api.md)
- [Hosted Endpoints](../../reference/hosted-endpoints.md)
- [TypeScript Client Events](../events/typescript-client.md)
- [Hosted TUI Runtime](../tui/hosted-runtime.md)
