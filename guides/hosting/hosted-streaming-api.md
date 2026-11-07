# Hosted Streaming API

The hosted streaming API is for UI and integration clients that submit queued work and active controls, observe canonical thread events, and answer interactive middleware requests.

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

The submitted `runConfig` may include harness settings for one execution:

```json
{
  "harnesses": [
    {
      "name": "SearchHarness",
      "config": {
        "maxResults": 5
      }
    }
  ]
}
```

The agent must already contain the matching registered harness and generated codec. See [Configure A Harness Per Run](../tools/configure-a-harness-per-run.md).

Accepted input returns `202 Accepted` with the backend-owned run identity:

```json
{
  "disposition": "queued",
  "threadExecutionId": "execution-123",
  "startedAt": "2026-07-15T10:15:30Z"
}
```

Observe committed SSE events to render output, completion, errors, and interactive requests. Keep the returned `threadExecutionId` for lifecycle correlation, steering, and interruption.

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
    "compaction": null
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
    "compaction": {
      "point": { "$type": "currentHead" },
      "preservation": { "$type": "previousTurns", "count": 5 },
      "strategy": { "$type": "summarizing" },
      "commitMode": 0
    },
    "continuation": 1
  }
}
```

`continuation: 1` is `CompactionContinuation.StopAfterCompaction`; `commitMode: 0` is soft and `1` is hard. Hosting gives explicit compaction normal run ownership and interruption semantics without creating a user message or entering a model turn.

## Read State Then Observe With SSE

First load authoritative runtime control state:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state
```

It returns `observedCursor`, the backend-owned `activeExecution`, and currently answerable `pendingRequests`. It does not return event history. Restore controls immediately, then connect SSE from the greatest contiguous cursor your client has successfully projected. Use sequence zero in the observed generation when no local projection exists:

```http
GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after=3:0
Accept: text/event-stream
```

The same stream catches up every committed event after the cursor and then remains attached for future commits. Each event includes its committed thread position as the SSE id:

```text
id: 3:43
event: agent-event
data: {"version":"1.0","type":"TEXT_DELTA","threadSequenceNumber":43,"text":"hello","messageId":"..."}
```

Process events serially and advance the applied cursor only after the consumer applies an event successfully. `observedCursor` classifies the initial catch-up boundary; it must never overwrite the applied cursor. On EOF or a connection error, reconnect with the last applied `generation:sequence` cursor. The server also accepts `Last-Event-ID` when `after` is absent. Comment heartbeats can be ignored. If observation fails after SSE headers are committed, the server closes the stream; it never fabricates an uncommitted error `AgentEvent`.

Hard compaction sends `event: thread-journal-rebased` with the previous and current generations, then ends observation. Clear the old projection, reload state, and reconnect at sequence zero of the current generation.

Semantic inputs use `/inputs`, interactive responses use `/responses`, and committed thread observation uses SSE. See [Hosted Lifecycle And Recovery](hosted-lifecycle-and-recovery.md) for the complete recovery contract.

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
  "name": "Short answer",
  "compaction": {
    "$type": "enabled",
    "compaction": {
      "point": { "$type": "currentHead" },
      "preservation": { "$type": "previousTurns", "count": 3 },
      "strategy": { "$type": "summarizing" },
      "commitMode": 1
    }
  }
}
```

Use `$type: "inherit"` to apply configured fork compaction, `$type: "disabled"` to skip it, or `$type: "enabled"` with an explicit specification. Omitting `compaction` inherits the server-side configuration.

## Control An Active Execution

Interrupt the active thread execution by submitting its semantic input:

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

Read `/state` immediately before cancellation and send its active execution id as `threadExecutionId`. Steering uses a `STEERING_INPUT` envelope with that same target and a `messages` array.

The `202` response has a `disposition`:

- `accepted`: the matching active execution owns the control
- `no_active_execution`: there is no current execution
- `active_execution_mismatch`: another execution is active; the response includes that `activeExecution`
- `active_input_not_steerable`: steering targeted active work that is not a message execution
- `execution_finishing`: steering acceptance has already closed

Do not automatically retry against a mismatched execution. It may be newer work started by another client.

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
  "choiceId": "allow_once",
  "feedback": null
}
```

The body must be a serialized `AgentEvent` envelope whose event implements `IResponseEvent`, such as `PermissionResponseEvent`, `ContinuationResponseEvent`, `ClarificationResponseEvent`, `ClientToolInvokeOutcomeEvent`, or an app-owned custom response event.

Middleware responses are not thread executions. Canonical request events are committed only after the request is externally answerable. `/state` also exposes currently pending requests, allowing a recovering client to restore interaction UI before historical replay completes. The response route requires an existing session/thread scope, an active thread runtime, and a waiter that accepts the response. If the scope does not exist, it returns `404`. If no runtime is active, the route returns a thread-runtime-not-active conflict. If the runtime exists but no waiter accepts the response, it returns `409` with the `RespondResult` status.

## Event Bodies

Hosted streaming uses `AgentEventSerializer` envelopes: `version`, `type`, and camelCase payload properties, with null values omitted.

This page intentionally shows contract-level examples only. For event families and publication semantics, see [Events](../../reference/events.md).
