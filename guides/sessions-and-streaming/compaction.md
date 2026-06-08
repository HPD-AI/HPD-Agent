# Compaction

Compaction reduces conversation history before a model call and can optionally compact the durable branch projection.

Use this mental model:

```text
strategy changes what the next model sees
retention changes what future branch projection keeps
fork compaction rewrites the new target branch before it is committed
```

There are three related surfaces:

- model-visible compaction: middleware reduces the non-system messages used for the next model turn
- durable branch-history compaction: hard retention removes messages from the projected branch history and may insert replacement messages
- fork compaction: a newly forked target branch is reduced before its initial branch history is saved

## Enable Compaction

Compaction is opt-in. Set `Enabled = true` when configuring compaction:

```csharp
builder.WithCompaction(config =>
{
    config.Enabled = true;
    config.Strategy = new MessageCountingCompactionOptions
    {
        TargetMessageCount = 50
    };
    config.Trigger = new CountCompactionTriggerOptions
    {
        TargetCount = 20,
        Threshold = 5
    };
    config.Retention = new PreserveBranchHistoryOptions();
});
```

This configuration performs soft compaction when the count trigger fires. The next model call sees reduced history, but durable branch history remains intact.

Per-run controls can force or skip compaction. `SkipCompaction` wins over `TriggerCompaction`.

## Strategies

The strategy decides what remains visible to the next model turn.

`MessageCountingCompactionOptions` keeps recent messages. Its default target is 50 messages.

`SummarizingCompactionOptions` summarizes older history and keeps recent messages. The default shape keeps 20 recent messages, resummarizes after 5 new messages, uses a single summary, and uses handoff-style summaries. A summarizing strategy can use a separate summarizer provider through `ClientProviderConfig? SummarizerProvider`; otherwise it can use the main chat client.

System messages are separated before reduction and added back before the model call.

Choose the smallest strategy that solves the pressure you are seeing:

| Pressure | Use |
| --- | --- |
| The chat is simply getting long | `MessageCountingCompactionOptions` with preserve retention. |
| Older turns matter, but exact wording does not | `SummarizingCompactionOptions` with preserve retention. |
| The branch projection itself must stay small | A strategy plus `CompactBranchHistoryOptions` or `DeleteCompactedMessagesOptions`. |
| Tool calls/results are involved | Add boundary options that keep tool-call groups intact. |
| A new fork should start lighter than its source branch | Fork compaction through `BranchForkOptions.CompactionIntent`. |
| One request needs full context for debugging or a critical decision | `AgentRunConfig.SkipCompaction = true`. |
| One request should compact before continuing | `AgentRunConfig.TriggerCompaction = true`. |

## Triggers

Triggers decide when compaction runs:

| Trigger | Behavior |
| --- | --- |
| `CountCompactionTriggerOptions` | Runs after message or message-turn count exceeds `TargetCount + Threshold`. |
| `TokenBudgetCompactionTriggerOptions` | Runs when the last observed input token count exceeds `TargetTokenBudget + TokenBudgetThreshold`. |
| `ContextWindowCompactionTriggerOptions` | Runs when the last observed input token count crosses a configured percentage of the context window. |
| `CompositeCompactionTriggerOptions` | Runs when any child trigger in `AnyOf` runs. |

Token and context-window triggers use usage observed from prior turns. They are not preflight token counters for the current turn.

## Retention

Retention decides what happens to durable branch history after model-visible compaction.

| Retention | Durable branch projection |
| --- | --- |
| `PreserveBranchHistoryOptions` | Preserves branch history. This is the default and safest mode. |
| `CompactBranchHistoryOptions` | Removes durable compacted messages and inserts replacement messages where the removed range began. |
| `DeleteCompactedMessagesOptions` | Removes durable compacted messages without replacement messages. |

Preserve retention is soft compaction. It changes what the model sees but does not remove projected branch messages.

Compact and delete retention are hard retention modes. They can change future `LoadBranchAsync(...)` projection and can make old message ids unavailable as fork points.

## Boundary Policies

Boundary policies control which durable messages are removed under hard retention:

| Boundary | Durable removal scope |
| --- | --- |
| `ExactCompactedMessagesBoundaryOptions` | Removes exactly the durable compacted message ids selected by compaction, excluding retained and system messages. |
| `IncludePreviousMessagesBoundaryOptions` | Includes previous non-system messages before the compacted range. |
| `IncludeMessageTurnBoundaryOptions` | Expands to messages in the same message turn. |
| `IncludeToolCallGroupBoundaryOptions` | Expands to matching tool-call and tool-result messages. |
| `CompositeCompactionBoundaryOptions` | Applies multiple boundary policies. |

Message-turn boundaries depend on projected message metadata such as `hpd.messageTurnId`.

## Events And State

Compaction uses three different records:

| Record | Meaning |
| --- | --- |
| `CompactionEvent` | Live middleware observability for skipped or performed compaction. |
| `CompactionStateData` | Branch-scoped persistent middleware state with last compaction, trigger counts, and usage observations. |
| `BranchHistoryCompactedEvent` | Durable branch event that changes branch projection under hard retention. |

`CompactionEvent` is useful for diagnostics and live UI. It is not the durable projection instruction.

`BranchHistoryCompactedEvent` is appended when hard retention is applied to a branch with a store. Projection applies it by removing `DurableCompactedMessageIds` and inserting any `ReplacementMessages`.

## Fork And Compact

A normal fork copies source messages through the fork point, copies branch-scoped middleware state, shares session-scoped state, and prepares target branch metadata in memory.

Fork compaction runs in the pre-commit fork middleware hook. If enabled, `CompactionMiddleware` reduces the target branch messages before the target branch's initial event document is saved.

The source branch is unchanged.

Fork compaction does not append a standalone `BranchHistoryCompactedEvent`. The target branch starts with the already-compacted initial history.

Direct in-process callers can override the fork compaction choice with `BranchForkOptions`:

```csharp
await agent.ForkBranchAsync(
    sessionId,
    sourceBranchId,
    newBranchId,
    fromMessageId,
    new BranchForkOptions
    {
        CompactionIntent = BranchForkCompactionIntent.Enabled,
        Metadata = new Dictionary<string, object>
        {
            ["name"] = "Compacted exploration"
        }
    },
    cancellationToken);
```

ASP.NET Core hosted fork requests do not currently include a per-request compaction intent. In hosted apps, fork compaction is controlled by the configured server-side agent and middleware pipeline unless the application exposes its own higher-level route.

## UI Guidance

For transcript views, render the projected branch messages as canonical.

After hard branch-history compaction:

- replacement messages appear where the compacted range used to be
- delete retention closes the gap without replacement messages
- the compaction event can be shown in an audit or debug lane
- compacted-away message ids may no longer be valid fork points

Do not render deleted compacted messages as ordinary transcript rows unless your application has a separate archival source.

Example projection:

```text
Before hard branch-history compaction:
  user-1, assistant-1, user-2, assistant-2

After compact retention with a replacement message:
  summary-1, user-2, assistant-2

After delete retention without replacement:
  user-2, assistant-2
```

## Related Pages

- [Sessions, Branches, And Events](../../concepts/sessions-branches-and-events.md)
- [Branch History And Forking](branch-history-and-forking.md)
- [Render An Event Stream](render-an-event-stream.md)
- [Live Vs Durable Events](../events/live-vs-durable-events.md)
- [Middleware State Persistence](../middleware/state-persistence.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
- [Hosted Endpoints](../../reference/hosted-endpoints.md)
- [Hosted TUI Runtime](../tui/hosted-runtime.md)
- [Subagents](../agents/subagents.md)
