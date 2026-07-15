# Hosted Lifecycle And Recovery

Hosted clients use one server-authoritative lifecycle for opening a thread, observing committed events, submitting work, reconnecting, and interrupting a run. The protocol uses HTTP for commands and state, plus resumable Server-Sent Events (SSE) for observation.

The runtime scope is always:

```text
agentId + sessionId + threadId
```

## The Lifecycle

For every initial open or recovery:

```text
GET /state
  -> render events
  -> remember latestSequenceNumber
  -> inspect activeRun

GET /events/live?after={latestSequenceNumber}
  -> apply each committed event
  -> acknowledge it locally
  -> advance the cursor only after application succeeds
```

Submitting input is a separate command:

```text
POST /inputs
  -> 202 { runtimeRunId, startedAt }
```

The response identifies the backend-owned run. Output and terminal state arrive as committed events on SSE. The submission request is not an output stream.

This design gives opening, reconnecting, process recovery, and returning to an old session the same code path. A client does not need a separate recovery mode.

## Load Authoritative State

Read the thread snapshot before opening SSE:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state
```

The response contains:

```json
{
  "latestSequenceNumber": 42,
  "activeRun": {
    "runtimeRunId": "run-123",
    "agentId": "assistant",
    "sessionId": "session-1",
    "threadId": "main",
    "status": "active"
  },
  "events": []
}
```

`events` is the committed thread history in sequence order. `latestSequenceNumber` is the cursor at the same snapshot boundary. `activeRun` is null when that agent does not own active work for the thread.

Use this endpoint instead of assembling lifecycle state from separate history and active-run requests. It prevents a run from changing between two reads.

## Resume Committed Events

Connect after the snapshot cursor:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events/live?after=42
Accept: text/event-stream
```

Each committed event includes its sequence number as the SSE id:

```text
id: 43
data: {"version":"1.0","type":"TEXT_DELTA","sequenceNumber":43,"text":"hello"}

```

The server sends only events whose sequence number is greater than `after`, in ascending order. It also sends comment heartbeats during quiet periods:

```text
: heartbeat

```

Clients should:

1. Process events serially.
2. Verify that the SSE id and event sequence agree.
3. Advance the local cursor only after the event has been applied successfully.
4. Ignore an event at or below the acknowledged cursor.
5. Reconnect with `after` set to the last acknowledged cursor after EOF or a transport failure.

Do not advance the cursor when a projection handler fails. Reconnecting from the previous cursor lets the client retry that event instead of silently losing it.

## Submit And Track A Run

Submit text or an input event through `/inputs`. A successful response is authoritative:

```json
{
  "runtimeRunId": "run-123",
  "startedAt": "2026-07-15T10:15:30Z"
}
```

Only one hosted run may be active for a thread. A conflicting submission returns `409` with `ThreadRunActive`.

Track the returned `runtimeRunId` until a matching terminal event is committed. If the client loses its local state, reload `/state`; do not invent a run id or assume that disconnecting stopped backend work.

## Interrupt Safely

Cancellation uses compare-and-interrupt:

1. Read `/state`.
2. If `activeRun` is null, there is nothing to interrupt.
3. Send that run's id as `expectedRuntimeRunId`.

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/interrupt
Content-Type: application/json

{
  "expectedRuntimeRunId": "run-123",
  "reason": "User cancelled the request."
}
```

The route returns `202` with a structured result:

| Status | Meaning |
| --- | --- |
| `accepted` | The expected active run received the interruption. |
| `already_terminal` | The expected run is already represented as terminal history. |
| `no_active_run` | No run is currently active and the expected id was not found as terminal. |
| `active_run_mismatch` | Another run is active; `activeRun` describes it. |

Treat `active_run_mismatch` as fresh state, not as permission to interrupt the newer run automatically. This comparison prevents a delayed Escape key, stale tab, or recovering client from cancelling work it did not intend to target.

## Recover After A Long Disconnect

The backend run is independent of the SSE connection. If a laptop sleeps, a proxy closes an idle stream, or the UI stays open for hours:

1. Keep the last fully applied sequence number.
2. Reconnect SSE with that number in `after`.
3. If local state is uncertain, reload `/state`, replace the projection with its committed events, and subscribe after its cursor.
4. Use `activeRun` from the snapshot to restore running/cancellation controls.

Closing the UI does not imply cancellation. Reopening the same scope hydrates committed output and discovers any still-active run through `/state`.

## Interactive Responses

Send a matching `IResponseEvent` envelope through:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
```

Preserve the request id. Responses are commands and do not use the SSE connection in the reverse direction.

::: warning Current committed-stream limitation
The hosted SSE endpoint reads the committed thread document. An event type reaches hosted clients only when it opts into thread persistence and has a thread projection. Built-in permission, clarification, continuation, and client-tool request records currently default to non-persistent events, so a hosted client cannot rely on receiving those request payloads through this committed stream yet. The `/responses` route exists, but the request-delivery side must be moved onto the committed lifecycle before hosted bidirectional interaction is complete.
:::

## Why There Is No Agent WebSocket

The hosted agent lifecycle intentionally has one transport model. The former agent WebSocket route and transport selection were removed because they did not share the snapshot, committed cursor, acknowledged replay, or authoritative submission contract.

Other HPD features may still use WebSockets for their own protocols, including bot adapters, realtime provider APIs, and the separate client-tool-provider connection. Those sockets are not the hosted agent event lifecycle.

## Related Pages

- [Hosted Streaming API](hosted-streaming-api.md)
- [Hosted Endpoints](../../reference/hosted-endpoints.md)
- [TypeScript Client Events](../events/typescript-client.md)
- [Hosted TUI Runtime](../tui/hosted-runtime.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
