# Compaction

Compaction reduces conversation history before a model call and can optionally compact the durable thread projection.

Use this mental model:

```text
strategy changes what the next model sees
retention changes what future thread projection keeps
fork compaction rewrites the new target thread before it is committed
```

There are three related surfaces:

- model-visible compaction: middleware reduces the non-system messages used for the next model turn
- durable thread-history compaction: hard retention removes messages from the projected thread history and may insert replacement messages
- fork compaction: a newly forked target thread is reduced before its initial thread history is saved

## Enable Compaction

Compaction is opt-in. Set `Enabled = true` when configuring compaction:

```csharp
builder.WithCompaction(config =>
{
    config.Enabled = true;
    config.Strategy = new MessageCountingCompactionOptions
    {
        PreserveRecentUserTurnCount = 10
    };
    config.Trigger = new CountCompactionTriggerOptions
    {
        TargetCount = 20,
        Threshold = 5
    };
    config.Retention = new PreserveThreadHistoryOptions();
});
```

This configuration performs soft compaction when the count trigger fires. The next model call sees reduced history, but durable thread history remains intact.

Per-run controls can force, disable, or tune compaction through `AgentRunConfig.Compaction`:

```csharp
await agent.RunAsync(
    "Continue from here.",
    runConfig: new AgentRunConfig
    {
        Compaction = new CompactionRunConfig
        {
            Mode = CompactionRunMode.Force,
            Behavior = CompactionBehavior.StopAfterCompaction
        }
    });
```

`CompactionRunMode.Disabled` wins over configured triggers for that run. `CompactionRunMode.Force` bypasses triggers and runs compaction before the model turn when a strategy is available. `CompactionBehavior.StopAfterCompaction` is useful for explicit compaction operations because it compacts the current thread and ends the run before a model call.

## Strategies

The strategy decides what remains visible to the next model turn.

`MessageCountingCompactionOptions` keeps recent user turns. Its default preserve target is 10 user turns. The framework translates that into the raw message suffix needed to keep each preserved user request with the assistant, tool-call, and tool-result messages that follow it.

`SummarizingCompactionOptions` summarizes older history and keeps recent user turns. The default shape preserves 5 recent user turns, resummarizes after 5 new messages, uses a single summary, and uses handoff-style summaries. A summarizing strategy can use a separate summarizer provider through `ClientProviderConfig? SummarizerProvider`; otherwise it can use the main chat client.

Set `PreserveRecentUserTurnCount = 0` when the result should retain no existing raw user-turn suffix. Zero is valid for both built-in strategies. For summarization, HPD still gives the reducer an internal sentinel so it can produce a summary when no real message is preserved; that sentinel is removed from the result and is never persisted or sent to the main chat model.

Both strategies also inherit explicit preservation boundaries from `CompactionStrategyOptions`:

```csharp
new SummarizingCompactionOptions
{
    PreserveRecentUserTurnCount = 0,
    PreserveFromMessageId = "message-42"
};
```

`PreserveFromMessageId` or `PreserveFromMessageTurnId` keeps the raw suffix beginning at that exact message or message turn. An explicit boundary takes precedence over `PreserveRecentUserTurnCount`. This is useful for user-directed "compact everything before here" operations.

System messages are separated before reduction and added back before the model call.

Choose the smallest strategy that solves the pressure you are seeing:

| Pressure | Use |
| --- | --- |
| The chat is simply getting long | `MessageCountingCompactionOptions` with preserve retention. |
| Older turns matter, but exact wording does not | `SummarizingCompactionOptions` with preserve retention. |
| The thread projection itself must stay small | A strategy plus `CompactThreadHistoryOptions`. |
| Tool calls/results are involved | Add boundary options that keep tool-call groups intact. |
| A new fork should start lighter than its source thread | Fork compaction through `ThreadForkOptions.Compaction`. |
| One request needs full context for debugging or a critical decision | `AgentRunConfig.Compaction = new() { Mode = CompactionRunMode.Disabled }`. |
| One request should compact before continuing | `AgentRunConfig.Compaction = new() { Mode = CompactionRunMode.Force }`. |

## Triggers

Triggers decide when compaction runs:

| Trigger | Behavior |
| --- | --- |
| `CountCompactionTriggerOptions` | Runs after message or message-turn count exceeds `TargetCount + Threshold`. |
| `ContextWindowCompactionTriggerOptions` | Runs when the last observed input token count crosses either a configured percentage of the context window or an explicit token count. |
| `CompositeCompactionTriggerOptions` | Runs when any child trigger in `AnyOf` runs. |

Context-window triggers use usage observed from prior turns. They are not preflight token counters for the current turn.

`ContextWindowCompactionTriggerOptions.ContextWindowSize` can be omitted when the active run supplies model context metadata:

```csharp
new AgentRunConfig
{
    ProviderKey = "openai",
    ModelId = "gpt-4.1",
    Compaction = new CompactionRunConfig
    {
        Trigger = new ContextWindowCompactionTriggerOptions
        {
            ThresholdMode = ContextWindowCompactionThresholdMode.Percentage,
            TriggerPercentage = 0.70
        },
        ModelContext = new ModelContextWindowOptions
        {
            ProviderKey = "openai",
            ModelId = "gpt-4.1",
            ContextWindow = 128000
        }
    }
};
```

For a fixed token threshold, use token-count mode:

```csharp
new ContextWindowCompactionTriggerOptions
{
    ThresholdMode = ContextWindowCompactionThresholdMode.TokenCount,
    TriggerTokenCount = 64_000
};
```

Hosted clients can preflight the same model-aware pressure with `POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/context-usage`. The endpoint returns `ThreadContextUsage` and does not start a run or mutate history.

## Retention

Retention decides what happens to durable thread history after model-visible compaction.

| Retention | Durable thread projection |
| --- | --- |
| `PreserveThreadHistoryOptions` | Preserves thread history. This is the default and safest mode. |
| `CompactThreadHistoryOptions` | Removes durable compacted messages and inserts replacement messages where the removed range began. |

Preserve retention is soft compaction. It changes what the model sees but does not remove projected thread messages.

Compact retention is hard compaction. It can change future `LoadThreadAsync(...)` projection and can make old message ids unavailable as fork points.

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
| `CompactionStateData` | Thread-scoped persistent middleware state with last compaction, trigger counts, and usage observations. |
| `ThreadHistoryCompactionCheckpointEvent` | Durable thread checkpoint written for soft and hard compaction. |

`CompactionEvent` is useful for diagnostics and live UI. It is not the durable projection instruction.

`ThreadHistoryCompactionCheckpointEvent` is appended for every successful compaction on a thread with a store. Hard checkpoints remove `DurableCompactedMessageIds` and insert any `ReplacementMessages`. Soft checkpoints leave durable messages intact and let clients choose how to display compacted ranges.

## Fork And Compact

A normal fork copies source messages through the fork point, copies thread-scoped middleware state, shares session-scoped state, and prepares target thread metadata in memory.

Fork compaction runs in the pre-commit fork middleware hook. If enabled, `CompactionMiddleware` reduces the target thread messages before the target thread's initial event document is saved.

The source thread is unchanged.

Fork compaction appends the same checkpoint event to the target thread. The source thread is unchanged; the target thread is born with a hard checkpoint that records which copied messages were compacted before commit.

Direct in-process callers can override the fork compaction choice with `ThreadForkOptions`:

```csharp
await agent.ForkThreadAsync(
    sessionId,
    sourceThreadId,
    newThreadId,
    fromMessageId,
    new ThreadForkOptions
    {
        Compaction = new ThreadForkCompactionOptions
        {
            Mode = ThreadForkCompactionMode.Enabled,
            PreferCache = true,
            Strategy = new MessageCountingCompactionOptions
            {
                PreserveRecentUserTurnCount = 3
            }
        },
        Metadata = new Dictionary<string, object>
        {
            ["name"] = "Compacted exploration"
        }
    },
    cancellationToken);
```

ASP.NET Core hosted fork requests use the same shape:

```json
{
  "newThreadId": "compacted-exploration",
  "fromMessageId": "msg-42",
  "compaction": {
    "mode": 1,
    "preferCache": true,
    "strategy": {
      "$type": "messageCounting",
      "preserveRecentUserTurnCount": 3
    }
  }
}
```

Fork compaction uses shared strategy options, but it is not a normal run compaction policy. It has no trigger, behavior, or soft retention setting. A fork compaction shapes the new fork target and leaves the source thread unchanged.

## UI Guidance

For transcript views, render the projected thread messages as canonical.

After hard thread-history compaction:

- replacement messages appear where the compacted range used to be
- delete retention closes the gap without replacement messages
- the compaction event can be shown in an audit or debug lane
- compacted-away message ids may no longer be valid fork points

Do not render deleted compacted messages as ordinary transcript rows unless your application has a separate archival source.

Example projection:

```text
Before hard thread-history compaction:
  user-1, assistant-1, user-2, assistant-2

After compact retention with a replacement message:
  summary-1, user-2, assistant-2

After delete retention without replacement:
  user-2, assistant-2
```

## Related Pages

- [Sessions, Threads, And Events](../../concepts/sessions-threads-and-events.md)
- [Thread History And Forking](thread-history-and-forking.md)
- [Render An Event Stream](render-an-event-stream.md)
- [Live Vs Durable Events](../events/live-vs-durable-events.md)
- [Middleware State Persistence](../middleware/state-persistence.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
- [Hosted Endpoints](../../reference/hosted-endpoints.md)
- [Hosted TUI Runtime](../tui/hosted-runtime.md)
- [Subagents](../agents/subagents.md)
