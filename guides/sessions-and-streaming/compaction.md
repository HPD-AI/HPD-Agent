# Compaction

Compaction creates a smaller semantic thread from an existing conversation. One specification states:

```text
point         where compaction stops
preservation  what history immediately before that point remains verbatim
strategy      whether selected history is removed or summarized
commit mode   whether the checkpoint is appended or the journal is replaced
```

Automatic, explicit, and fork compaction use the same `IThreadCompactionEngine`. `CompactionMiddleware` adapts that engine to iteration and fork lifecycle hooks; `Agent` does not contain compaction-specific execution logic.

## Configure Compaction

Configure the default automatic and fork policies when building the agent:

```csharp
builder.WithCompaction(config =>
{
    config.Automatic = new AutomaticCompactionPolicy
    {
        Trigger = new ContextPercentageCompactionTrigger(
            TotalInputTokens: 120_000,
            Percentage: 0.70),
        Compaction = new CompactionSpecification
        {
            Point = new CompactAtCurrentHead(),
            Preservation = new PreservePreviousTurns(5),
            Strategy = new SummarizingCompaction(),
            CommitMode = CompactionCommitMode.Soft
        }
    };

    config.ForkCompaction = new CompactionSpecification
    {
        Point = new CompactAtCurrentHead(),
        Preservation = new PreservePreviousTurns(3),
        Strategy = new SummarizingCompaction(),
        CommitMode = CompactionCommitMode.Hard
    };
});
```

`CompactionConfig.Automatic = null` disables automatic compaction while retaining explicit and fork compaction. A non-null `AgentRunConfig.Compaction.Automatic` replaces the configured automatic policy for one run; otherwise the configured policy is used.

All compaction configuration records are explicitly polymorphic through `$type` discriminators and are registered with the framework JSON contexts for source-generated, Native AOT-friendly serialization.

## Automatic Triggers

Automatic triggers are evaluated in `BeforeIterationAsync` against the messages that are about to reach the provider:

| Trigger | Fires when |
| --- | --- |
| `TurnCountCompactionTrigger(Turns)` | The current model input contains at least that many distinct turns. |
| `InputTokenCompactionTrigger(InputTokens)` | Estimated input tokens reach the absolute threshold. |
| `ContextPercentageCompactionTrigger(TotalInputTokens, Percentage)` | Estimated input tokens reach the stated fraction of the explicit total input-token capacity. |

A percentage always includes its denominator. For example, 70 percent of a model with a 120,000-token input limit is:

```csharp
new ContextPercentageCompactionTrigger(120_000, 0.70)
```

Do not substitute a generic context-window number when the provider exposes a smaller input-token limit.

## Compaction Point

The point is the exclusive end of the history being considered for compaction:

| Point | Meaning |
| --- | --- |
| `CompactAtCurrentHead` | Compact history before the current thread head. |
| `CompactAtMessage(MessageId, ExpectedJournalGeneration)` | Compact history before the selected message. |
| `CompactAtTurn(TurnId, ExpectedJournalGeneration)` | Compact history before the selected turn. |

Message and turn points may carry the journal generation from which the user selected them. The engine rejects the request if hard compaction has since replaced that generation. This prevents a stale UI selection from targeting a different message with a coincidentally reused sequence.

The engine expands a message point to the start of its turn when turn metadata is available, so compaction does not split that turn accidentally.

## Preservation

Preservation applies only to history before the compaction point:

| Preservation | Result |
| --- | --- |
| `PreserveNoPreviousHistory` | Select all history before the point. |
| `PreservePreviousTurns(Count)` | Keep the most recent complete turns before the point verbatim. |
| `PreservePreviousUserMessages(Limit)` | Carry selected prior user messages forward without retaining all intervening assistant activity. |

`PreservePreviousUserMessages` accepts either:

```csharp
new PreviousItemCountLimit(3)
new PreviousTokenBudgetLimit(2_000)
```

The carried user messages receive new message identities while recording their source identities in the checkpoint. This supports a summary-plus-user-prompts shape without pretending that tool calls or assistant messages were preserved.

Preserving turns keeps the complete semantic turn. Registered event and projection relationships—not a hard-coded list of tool event types—determine the associated history.

## Strategies

The strategy determines what replaces selected history:

### Remove selected history

```csharp
Strategy = new RemovalCompaction()
```

The selected history contributes no replacement message.

### Summarize selected history

```csharp
Strategy = new SummarizingCompaction
{
    Provider = new ClientProviderConfig
    {
        ProviderKey = "openai",
        ModelName = "gpt-4.1-mini"
    },
    Instructions = "Preserve decisions, file paths, failures, and exact next actions."
}
```

When `Provider` is absent, the configured summarizer client is used, falling back to the active chat client. The built-in instructions produce a continuation handoff rather than a conversational answer.

## Soft And Hard Commit Modes

Both modes produce a `ThreadHistoryCompactionCheckpointEvent`. They differ in what happens to the canonical journal.

### Soft

Soft compaction appends the checkpoint to the current generation:

```text
generation N:
  original events
  checkpoint
  future events
```

The canonical source events remain readable. Model projection applies the checkpoint and uses the compacted semantic result. Transcript products may retain, collapse, or hide the compacted range according to their display policy.

Use soft mode when full source history must remain available.

### Hard

Hard compaction atomically replaces the complete canonical journal:

```text
generation N                         generation N + 1
  complete original journal    ->     thread seed
                                      checkpoint
                                      authoritative runtime seeds
                                      resulting semantic messages
```

Every event in the previous generation is removed from the active journal. The replacement journal contains only the semantic result required by the specification, plus canonical facts needed to keep the thread operable.

Hosting reseeds an active run and answerable pending interactions during replacement. Escape, interruption, and interactive responses therefore continue to target authoritative runtime state after hard compaction.

Hard compaction resets sequence numbering inside a new generation. Consumers must track `ThreadJournalCursor`, not a sequence number alone.

Use hard mode when the old journal must be permanently replaced and the compacted semantic result should become the complete canonical history.

## Explicit Compaction

Explicit compaction is a dedicated input operation. It creates no user message and does not enter a model turn:

```csharp
await agent.RunAsync(new CompactThreadInputEvent
{
    SessionId = sessionId,
    ThreadId = threadId,
    Request = new ThreadCompactionRequest
    {
        Compaction = new CompactionSpecification
        {
            Point = new CompactAtMessage(
                selectedMessageId,
                ExpectedJournalGeneration: selectedGeneration),
            Preservation = new PreservePreviousTurns(2),
            Strategy = new SummarizingCompaction(),
            CommitMode = CompactionCommitMode.Hard
        },
        Continuation = CompactionContinuation.StopAfterCompaction
    }
});
```

`Continue` allows execution to proceed after compaction. `StopAfterCompaction` completes the compaction operation without making a provider request.

Hosted clients submit the same `COMPACT_THREAD_INPUT` envelope through the normal input endpoint.

## Fork Compaction

Fork compaction shapes only the new target thread. The source thread remains unchanged.

The fork option is an explicit union:

```csharp
new InheritThreadForkCompaction()
new DisableThreadForkCompaction()
new ApplyThreadForkCompaction(specification)
```

Example:

```csharp
await agent.ForkThreadAsync(
    sessionId,
    sourceThreadId,
    newThreadId,
    fromMessageId,
    new ThreadForkOptions
    {
        Compaction = new ApplyThreadForkCompaction(
            new CompactionSpecification
            {
                Point = new CompactAtCurrentHead(),
                Preservation = new PreservePreviousTurns(3),
                Strategy = new SummarizingCompaction(),
                CommitMode = CompactionCommitMode.Hard
            })
    },
    cancellationToken);
```

`Inherit` uses `CompactionConfig.ForkCompaction`. `Disabled` skips it. `Enabled` supplies the exact specification. Subagents that fork from a parent thread use this same contract; there is no separate subagent compaction system.

## Events

Compaction publishes two event categories:

| Event | Purpose |
| --- | --- |
| `CompactionEvent` | Canonical lifecycle facts: `Started`, `Skipped`, `Failed`, and `Completed`, including origin and continuation. |
| `ThreadHistoryCompactionCheckpointEvent` | The authoritative successful semantic change. |

The checkpoint records:

- compaction point and expected generation;
- preservation policy;
- compacted and preserved message identities;
- carried user-message source identities;
- messages after the point;
- replacement messages;
- strategy descriptor;
- soft or hard commit mode;
- compaction timestamp.

There is no persistent compaction middleware state. Trigger decisions derive from the actual model input, and successful projection derives from canonical checkpoints.

## Client Recovery After Hard Compaction

A hard replacement invalidates every cursor from the previous generation. Hosted observation reports a typed journal-rebase condition containing the old and current generations.

Clients then:

1. Stop observation of the old generation.
2. Reload runtime control state.
3. Clear the projection built from the old journal.
4. Observe the new generation from sequence zero.
5. Continue from the new applied cursor.

The HPD TypeScript headless controller and hosted .NET TUI perform this rehydration automatically.

## Related Pages

- [Canonical Thread Journal](canonical-thread-journal.md)
- [Thread History And Forking](thread-history-and-forking.md)
- [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
- [Render An Event Stream](render-an-event-stream.md)
- [TypeScript Client Events](../events/typescript-client.md)
- [Subagents](../agents/subagents.md)
