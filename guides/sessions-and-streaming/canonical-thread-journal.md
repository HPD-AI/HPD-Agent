# Canonical Thread Journal

Every projectable thread-scoped `AgentEvent` has one canonical lifecycle:

```text
propose event
  -> validate scope, identity, codec, and size
  -> atomically append to the configured ISessionStore
  -> receive its authoritative Generation + ThreadSequenceNumber
  -> publish that exact committed value
  -> replay that exact value later
```

The journal is the public source of truth for transcript reconstruction, model projection, reconnect catch-up, multi-client convergence, run recovery, and custom offline projections.

## Canonical Invariants

A committed event preserves:

- concrete event type from the selected application composition;
- payload and stable `EventId`;
- session and thread scope;
- authoritative journal generation and positive `ThreadSequenceNumber`;
- relative journal order.

Historical, catch-up, and live delivery preserve the event value. Delivery context may control batching or animation independently of the payload.

The session store assigns `ThreadSequenceNumber` at commit. A normal append remains in the current generation. An atomic journal replacement increments the generation and assigns fresh contiguous sequence numbers. Generic event routing keeps its infrastructure order separate from the domain event.

Consumers resume with `ThreadJournalCursor(Generation, SequenceNumber)`. A sequence number alone is ambiguous after a hard compaction replaces the journal.

## `ISessionStore`

`ISessionStore` remains one storage-provider abstraction. Providers implement operations with bounded semantics:

```csharp
ValueTask<ThreadEventAppendResult> AppendThreadEventsAsync(
    ThreadKey thread,
    IReadOnlyList<AgentEvent> events,
    ThreadAppendCondition condition = default,
    CancellationToken cancellationToken = default);

ValueTask<ThreadJournalReplaceResult> ReplaceThreadEventsAsync(
    ThreadKey thread,
    IReadOnlyList<AgentEvent> events,
    ThreadJournalCursor expectedCursor,
    CancellationToken cancellationToken = default);

ValueTask<ThreadDescriptor?> GetThreadAsync(...);
IAsyncEnumerable<ThreadDescriptor> ListThreadsAsync(...);
ValueTask<ThreadEventHead?> GetThreadEventHeadAsync(...);
IAsyncEnumerable<ThreadEventBatch> ReadThreadEventsAsync(...);
IAsyncEnumerable<ThreadEventBatch> ObserveThreadEventsAsync(...);
```

`ReadThreadEventsAsync` uses an exclusive cursor, optional inclusive sequence boundary within that generation, and a server-enforced batch count. `ObserveThreadEventsAsync` catches up from the cursor, registers without a missed-wakeup gap, and then waits for future commits.

`ReplaceThreadEventsAsync` is an optimistic, atomic generation change. It validates the expected cursor, replaces the complete active journal, increments the generation, and wakes observers only after the replacement is readable. Observers attached to the prior generation receive `ThreadJournalReplacedException` and must rehydrate the new generation from sequence zero.

The commit notification is only a wakeup. Readers always reread the committed journal as the authoritative payload source.

## Descriptors And Explicit Projection

`GetThreadAsync` and `ListThreadsAsync` return lightweight `ThreadDescriptor` values without reconstructing model history.

Full semantic reconstruction is intentionally explicit:

```csharp
var thread = await store.ProjectThreadAsync(
    sessionId,
    threadId,
    ThreadProjectionPurpose.ModelContext,
    cancellationToken);
```

A consumer that needs canonical events processes bounded batches and discards each raw batch after successful projection. Full conversation display does not require retaining every event object.

## Built-In Stores

`InMemorySessionStore` uses append-oriented per-thread segments and commit signals, so append cost follows the committed batch.

`FileSessionStore` uses a segmented layout:

```text
sessions/{sessionId}/
  session.meta.json
  threads/{threadId}/
    thread.descriptor.json
    journal.index
    journal/
      generation
      segment-00000000000000000001.events
      segment-00000000000000001025.events
```

The journal is canonical. The descriptor and index are derived and rebuildable. The file store detects incomplete final frames, rebuilds projections when required, and uses independent per-thread commit gates.

Applications configure `FileSessionStore` for filesystem-backed persistence or provide another `ISessionStore` implementation with the same journal guarantees.

SQLite, PostgreSQL, remote, and application-defined stores can implement the same contract with their native transactions, indexes, and notification mechanisms. No storage technology appears in event or HTTP semantics.

## Canonical Versus Process-Local Events

A thread-scoped projectable event published through the canonical thread publisher is durable by definition.

Truly ephemeral diagnostics use tracing, metrics, struct events, or an explicitly process-local event rail. Stateless agents may emit the same event types with `ThreadSequenceNumber == 0`, but those values are not committed and have no replay guarantee.

## Failure Rules

- Append failure publishes no canonical event.
- Expected-cursor conflicts are typed `ThreadAppendConflictException` failures.
- A cursor ahead of the journal head is a `ThreadCursorConflictException`.
- A replaced generation terminates old observation with `ThreadJournalReplacedException`.
- Deletion terminates observation with `ThreadDeletedException`.
- Unknown or corrupt canonical events fail at their position; consumers do not silently skip them.
- A consumer advances its cursor only after successful projection.

## Related Pages

- [Sessions, Threads, And Events](../../concepts/sessions-threads-and-events.md)
- [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md)
- [Thread History And Forking](thread-history-and-forking.md)
- [Serialization And Registration](../events/serialization-and-registration.md)
