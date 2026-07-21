# Thread History And Forking

Threads let one session hold multiple replayable paths. A thread can be created directly, forked from a message id, updated for UI metadata, listed with sibling/tree information, and deleted subject to thread protection rules.

There are two public ways to work with threads:

- Direct in-process `Agent` APIs, where your code calls methods on a built `Agent` that has a session store.
- ASP.NET Core hosted APIs, where clients use HTTP routes and DTOs exposed by `MapHPDAgentApi(...)`.

## Direct Agent API

Use direct APIs when your application owns the `Agent` instance:

```csharp
var sessionId = await agent.CreateSessionAsync("review-session");

await agent.RunAsync(
    "Draft the first answer.",
    sessionId: sessionId,
    threadId: "main");

var forkId = await agent.ForkThreadAsync(
    sessionId,
    sourceThreadId: "main",
    newThreadId: "short-answer",
    fromMessageId: messageId);
```

Direct APIs require a configured session store. `CreateSessionAsync(...)` creates the default `main` thread. `ForkThreadAsync(...)` returns the new thread id and can also accept `ThreadForkOptions` for programmatic fork behavior such as fork compaction.

## ASP.NET Core Hosted Routes

Thread read, update, delete, content, and event-log routes are scoped by `sessionId` and `threadId`. Thread create and fork routes also include `agentId` because they need the hosted runtime configuration for that agent scope.

| Operation | Route |
| --- | --- |
| List threads | `GET /sessions/{sessionId}/threads` |
| Get thread | `GET /sessions/{sessionId}/threads/{threadId}` |
| Create thread | `POST /agents/{agentId}/sessions/{sessionId}/threads` |
| Fork thread | `POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/fork` |
| Update thread | `PATCH /sessions/{sessionId}/threads/{threadId}` |
| Delete thread | `DELETE /sessions/{sessionId}/threads/{threadId}?recursive=false` |
| Observe thread events | `GET /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after={appliedCursor}` |
| Get siblings | `GET /sessions/{sessionId}/threads/{threadId}/siblings` |

## Hosted Create A Thread

Creating a thread accepts a `CreateThreadRequest` with a thread id plus optional name, description, tags, and metadata. If the thread id is blank, the service can generate one.

Creating a thread with an existing id returns a conflict. A thread created this way starts as its own path rather than a fork from another thread.

## Hosted Fork From A Message

Forking accepts a `ForkThreadRequest`:

```json
{
  "newThreadId": "try-short-answer",
  "fromMessageId": "message-id-to-fork-from",
  "name": "Short answer",
  "description": "A shorter response path",
  "tags": ["draft"],
  "metadata": {
    "uiColor": "green"
  }
}
```

`fromMessageId` is required. If the message id is not present on the source thread, the hosted API returns a validation error.

The new thread records its source thread and fork point. Persistent thread-scoped middleware state is copied to the new thread when the fork is committed and then evolves independently.

## Hosted Update Thread Metadata

Thread updates accept optional name, description, tags, and metadata. Metadata is merged: values add or overwrite keys, and null removes keys.

Use thread metadata for UI labels, filters, annotations, and runtime hints that belong to a single path.

## Delete Threads

The `main` thread is protected and cannot be deleted.

In the hosted API, deleting a thread with children returns a conflict unless `recursive=true`. Recursive deletion must also be enabled by server configuration. If recursive deletion is disabled, the API returns a validation error even when the request includes `recursive=true`.

Hosted thread deletion uses a thread operation lock. A lock conflict means another exclusive thread mutation is in progress; it does not mean an agent run is active.

## Read Thread Events

Hosted clients open the thread observation route with their last successfully applied position. The SSE stream first yields canonical catch-up events and then remains attached for future commits. Starting at `after=0` reconstructs the complete thread incrementally.

In-process consumers use `ISessionStore.ReadThreadEventsAsync(...)` for finite, bounded cursor reads or `ObserveThreadEventsAsync(...)` for catch-up plus future commits. Both surfaces decode the same canonical `AgentEvent` values in ascending order within one journal generation.

Thread descriptors, lists, and existence checks use storage-native metadata and do not project the complete journal. Full semantic reconstruction is an explicit projection operation.

For UI projection guidance, see [Render An Event Stream](render-an-event-stream.md). For storage and observation guarantees, see [Canonical Thread Journal](canonical-thread-journal.md).

## Thread History Compaction

Hard thread-history compaction atomically replaces the complete journal. Its new generation contains `THREAD_HISTORY_COMPACTION_CHECKPOINT`, the configured semantic result, and any authoritative runtime facts that must survive the replacement.

Soft compaction appends the same checkpoint without deleting source events, so clients can retain, collapse, or mark compacted ranges during hydration.

For the full model, see [Compaction](compaction.md).

## Forking After Compaction

Forking uses a message id in the current journal generation. A hard replacement permanently removes older message ids. A stale message or turn selection should include its expected generation and fails with a cursor conflict instead of targeting a coincidentally similar replacement.

Use projected thread messages as the source of forkable message ids.

## Fork And Compact

Fork compaction uses the shared compaction engine during the pre-commit fork hook. The target is compacted after source projection and before its initial journal is saved.

The source thread is unchanged. The target thread starts with the compacted semantic history and a `THREAD_HISTORY_COMPACTION_CHECKPOINT` describing the fork result.

Hosted `ForkThreadRequest.Compaction` accepts the same inherit, disabled, or enabled union as `ThreadForkOptions.Compaction`. Omitting it inherits the server-side configuration.

The middleware hook for this phase is `BeforeThreadForkCommitAsync`. It receives the source thread, the in-memory target thread, the fork point, and `ThreadForkOptions`. This is the place for middleware that needs to change the target thread before it becomes durable. After the fork is committed, durable thread events such as `THREAD_CREATED` with fork metadata and later `THREAD_UPDATED` changes describe the committed thread state; they are not mutation hooks.

## Sibling Navigation

The hosted sibling endpoint returns thread DTOs with tree and sibling fields such as sibling index, total siblings, previous sibling id, next sibling id, original thread id, and total forks.

Current implementation returns full `ThreadDto` values for siblings. Avoid depending on a separate lightweight sibling DTO until that contract is reconciled.
