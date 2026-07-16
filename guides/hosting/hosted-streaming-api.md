# Hosted Streaming API

The hosted streaming API is for UI and integration clients that submit input, observe canonical thread events, interrupt active runs, and answer interactive middleware requests.

All runtime routes use:

```text
/agents/{agentId}/sessions/{sessionId}/threads/{threadId}
```

That scope identifies the hosted runtime agent and thread path.

## Submit Input

Submit simple text:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/inputs
Content-Type: application/json

{
  "text": "Explain this thread in one sentence.",
  "runConfig": {}
}
```

If the JSON body has no `type` property and has a non-empty `text` property, hosting treats it as a convenience text request and converts it to a `UserMessagesInputEvent`.

You can also submit an event envelope:

```json
{
  "version": "1.0",
  "type": "USER_MESSAGES_INPUT",
  "messages": [
    {
      "role": "user",
      "text": "Continue from here."
    }
  ]
}
```

`messages` is optional on `USER_MESSAGES_INPUT`. Normal user submissions should include at least one message. Omitting it or sending an empty array asks the runtime to resume the scoped thread with the supplied run configuration; the thread must already have history.

Accepted input returns `202 Accepted` with the backend-owned run identity:

```json
{
  "runtimeRunId": "run-123",
  "startedAt": "2026-07-15T10:15:30Z"
}
```

Observe committed SSE events to render output, completion, errors, and interactive requests. Keep the returned `runtimeRunId` for lifecycle state and safe interruption.

Only one active hosted run is allowed per thread. A second input submitted to the same `sessionId + threadId` while a run is active returns a conflict with a thread-run-active error.

## Estimate Context Usage

Hosted clients can ask the runtime how much context the current thread is using for a candidate run configuration:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/context-usage
Content-Type: application/json

{
  "runConfig": {
    "providerKey": "openai",
    "modelId": "gpt-4.1",
    "compaction": {
      "modelContext": {
        "providerKey": "openai",
        "modelId": "gpt-4.1",
        "contextWindow": 128000
      }
    }
  }
}
```

The response is `ThreadContextUsage`:

```json
{
  "sessionId": "session-1",
  "threadId": "main",
  "providerKey": "openai",
  "modelId": "gpt-4.1",
  "contextWindow": 128000,
  "effectiveInputTokens": 64000,
  "usageRatio": 0.5,
  "isEstimate": false,
  "source": "last-observed-provider-usage"
}
```

This endpoint does not start a run or mutate thread history. Use it for model-switch warnings, context meters, and deciding whether to submit an explicit compaction command.

## Explicit Compaction

Submit a dedicated compaction input through the normal hosted input route:

```json
{
  "version": "1.0",
  "type": "COMPACT_THREAD_INPUT",
  "request": {
    "continuation": 1
  }
}
```

`continuation: 1` is `CompactionContinuation.StopAfterCompaction`. The request may also override strategy, retention, and model-context metadata. Hosting gives it normal run ownership and interruption semantics without creating a user message or entering a model turn.

## Read State Then Observe With SSE

First load authoritative runtime control state:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state
```

It returns `observedHead`, the backend-owned `activeRun`, and currently answerable `pendingRequests`. It does not return event history. Restore controls immediately, then connect SSE from the greatest contiguous position your client has successfully projected. Use zero when no local projection exists:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after=0
Accept: text/event-stream
```

The same stream catches up every committed event after the cursor and then remains attached for future commits. Each event includes its committed thread position as the SSE id:

```text
id: 43
event: agent-event
data: {"version":"1.0","type":"TEXT_DELTA","threadSequenceNumber":43,"text":"hello","messageId":"..."}
```

Process events serially and advance the applied cursor only after the consumer applies an event successfully. `observedHead` classifies the initial catch-up boundary; it must never overwrite the applied cursor. On EOF or a connection error, reconnect with the last applied cursor. The server also accepts `Last-Event-ID` when `after` is absent. Comment heartbeats can be ignored. If observation fails after SSE headers are committed, the server closes the stream; it never fabricates an uncommitted error `AgentEvent`.

Input, interruption, and interactive responses use their HTTP routes. Committed thread observation uses SSE. See [Hosted Lifecycle And Recovery](hosted-lifecycle-and-recovery.md) for the complete recovery contract.

## Render Nested Activity

SSE delivers a linear committed stream. A hosted client can project that stream into a transcript, timeline, workflow tree, subagent view, or permission queue by grouping events with fields such as `messageId`, `callId`, `permissionId`, `traceId`, `spanId`, `parentSpanId`, `eventFlowId`, and agent `metadata`.

Workflow and subagent events may appear in the same observation stream as parent agent events when child execution is routed through the parent event coordinator. Treat the transport as delivery only; use [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md) for UI projection guidance.

## Fork Threads

Hosted clients fork threads with:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/fork
Content-Type: application/json

{
  "newThreadId": "try-short-answer",
  "fromMessageId": "message-id-to-fork-from",
  "name": "Short answer"
}
```

`ForkThreadRequest` does not include a per-request compaction intent. If fork compaction is enabled for the hosted agent, it is controlled by the server-side agent and middleware configuration. Clients should read the created thread, thread projection, or thread events after the fork instead of assuming the target thread retained every raw copied message.

## Interrupt A Run

Interrupt the active thread run:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/interrupt
Content-Type: application/json

{
  "expectedRuntimeRunId": "run-123",
  "reason": "User cancelled the request.",
  "eventFlowId": "optional-flow-id"
}
```

Read `/state` immediately before cancellation and send its active run id as `expectedRuntimeRunId`. The body can also include an `INTERRUPTION_REQUEST` event envelope. Omitting a JSON object creates a default user interruption reason.

The `202` response has one of four statuses:

- `accepted`: the expected active run received the interruption
- `already_terminal`: the expected run has already completed, failed, or been cancelled
- `no_active_run`: there is no current run and the expected run is not terminal history
- `active_run_mismatch`: another run is active; the response includes that `activeRun`

Do not automatically retry against a mismatched run. It may be newer work started by another client.

## Answer Interactive Middleware

HTTP clients answer bidirectional requests through one response route:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
Content-Type: application/json

{
  "version": "1.0",
  "type": "PERMISSION_RESPONSE",
  "permissionId": "permission-id-from-request",
  "sourceName": "PermissionMiddleware",
  "approved": true,
  "choice": "ask"
}
```

The body must be a serialized `AgentEvent` envelope whose event implements `IResponseEvent`, such as `PermissionResponseEvent`, `ContinuationResponseEvent`, `ClarificationResponseEvent`, `ClientToolInvokeOutcomeEvent`, or an app-owned custom response event.

Middleware responses are not thread runs. Canonical request events are committed only after the request is externally answerable. `/state` also exposes currently pending requests, allowing a recovering client to restore interaction UI before historical replay completes. The response route requires an existing session/thread scope, an active thread runtime, and a waiter that accepts the response. If the scope does not exist, it returns `404`. If no runtime is active, the route returns a thread-runtime-not-active conflict. If the runtime exists but no waiter accepts the response, it returns `409` with the `RespondResult` status.

## Event Bodies

Hosted streaming uses `AgentEventSerializer` envelopes: `version`, `type`, and camelCase payload properties, with null values omitted.

This page intentionally shows contract-level examples only. For event families and publication semantics, see [Events](../../reference/events.md).
