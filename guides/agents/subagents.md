# Subagents

A subagent is a real HPD agent exposed to a parent agent as a tool. The parent model sees one generated function with an `input` argument. By default, when the model calls it, HPD builds or loads the child agent, creates an agent-owned child thread using the configured context policy, streams child events through the parent event path, and returns the child answer as the tool result.

Use subagents when the parent should delegate to a specialist during normal tool calling. Use multi-agent workflows when the whole task should follow an explicit graph of roles and routes.

## Smallest Setup

Create a tool harness method marked with `[SubAgent]` and return a `SubAgent` definition:

```csharp
using HPD.Agent;

public sealed class SpecialistTools
{
    [SubAgent]
    public SubAgent Summarizer() =>
        SubAgent.FromConfig(
            "specialist.summarizer",
            "summarizer",
            "Summarizes a request before the parent answers.",
            new AgentConfig
            {
                Name = "Summarizer",
                SystemInstructions = "Summarize the user's request in three concise bullets.",
            });
}
```

Register the harness like any other tool harness:

```csharp
var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithToolHarness<SpecialistTools>()
    .BuildAsync();
```

The first argument is the durable child `AgentId`; the second is the model-facing tool name.
`SubAgent.FromConfig(...)` defaults to `SubAgentContextPolicy.Handoff`. Each call
creates a hidden child thread and passes the parent conversation as handoff
context unless the author selects a different context policy.

`AgentBuilder` provides an in-memory session store by default. Use `WithSessionStore(...)` when subagent threads should survive process restarts or be visible to another host.

## Tool Shape

By default, the generated tool takes one model-facing argument:

```json
{
  "input": "Summarize this request."
}
```

The subagent name becomes the tool name. The description becomes the tool description, so write it as guidance for the parent model. Subagent tools require permission by default.

## Control Nested Availability

Each subagent capability controls the agent depths from which that particular tool may be invoked. The default is root-only, so registering the same harness on a child does not accidentally make a subagent recursively call itself:

```text
root (depth 0) sees researcher
└─ researcher (depth 1) does not see researcher
```

No configuration is required for this safe default. Permit intentional nesting with `WithAvailability(...)`:

```csharp
return SubAgent.FromConfig(
    "research/coordinator",
    "coordinator",
    "Coordinates specialized research agents.",
    coordinatorConfig)
    .WithAvailability(SubAgentAvailability.ThroughDepth(2));
```

`ThroughDepth(2)` lets agents at depths zero and one invoke the capability, creating children at depths one and two. The tool is hidden at depth two. Use `SubAgentAvailability.AnyAllowedDepth` when the capability should remain available until the agent-wide `AgentConfig.MaxSubAgentDepth` ceiling is reached.

Availability is enforced both in the model-facing tool projection and again during invocation. A stale or manually constructed call therefore cannot bypass the policy. The per-capability policy may be stricter than `MaxSubAgentDepth`, but never less restrictive.

## Configure Direct Child Runs

`SubAgentRunConfig` is the per-input configuration for every direct child
invoked while processing that parent input:

```csharp
var childRun = new SubAgentRunConfig
{
    SystemInstructions = new SystemInstructionsRunConfig
    {
        Append = "Report concrete review findings first."
    },
    Security = new AgentSecurityRunConfig
    {
        Approval = AgentApprovalPolicy.ReviewProtectedActions
    }
};

await parentAgent.RunAsync(
    "Review the current change.",
    subAgentRunConfig: childRun);
```

The child still owns its complete `AgentConfig`. `SubAgentRunConfig` provides
per-invocation values, and parent and child security authority is intersected
so a child cannot silently broaden the controller's permissions.

There is no implicit inheritance of the parent's full `AgentRunConfig`. Pass
only the portable child behavior the invocation should receive.

`AgentRunConfig.Harnesses` is not inherited implicitly. Configure direct child harness settings explicitly through `SubAgentRunConfig.Harnesses`:

```csharp
var childRun = new SubAgentRunConfig();

childRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 5
    });

await parentAgent.RunAsync(
    "Research this question",
    subAgentRunConfig: childRun);
```

The override is resolved against the child's registered harness owner. It does not copy the parent's harness identity or configuration handle. The child keeps its own dependencies, middleware, construction settings, and configuration baseline.

Use `SubAgentRunDefaults.Harnesses` when a child should intentionally pass a harness setting to its own descendants:

```csharp
var descendantDefaults = new SubAgentRunDefaults();

descendantDefaults.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 3
    });

var childRun = new SubAgentRunConfig
{
    DescendantDefaults = descendantDefaults
};
```

The equivalent serialized direct-child setting is:

```yaml
subAgentRunConfig:
  harnesses:
    - name: SearchHarness
      config:
        maxResults: 5
```

The child must register the named harness. Missing or ambiguous owners fail with a child-specific configuration path before a child harness tool executes.

For synchronous calls, the tool result is the child agent's text response. HPD also records subagent metadata on the tool result, including `subAgentStatus`, `subAgentSessionId`, `subAgentThreadId`, `subAgentName`, and `invocationId`.

## Invocation Mode

Subagents are synchronous by default: the parent model calls the tool, HPD waits for the child agent, and the tool result is the child text.

For long-running delegation, set an invocation mode policy on the `SubAgent` definition:

```csharp
[SubAgent]
public SubAgent Reviewer() =>
    SubAgent.FromConfig(
        "specialist.reviewer",
        "reviewer",
        "Reviews a draft and reports actionable issues.",
        reviewerConfig,
        contextPolicy: SubAgentContextPolicy.Handoff,
        metadata: null,
        invocationModePolicy: AgentInvocationModePolicy.ModelChoice,
        operationNotification: null);
```

Policies:

| Policy | Tool Shape | Tool Result |
| --- | --- | --- |
| `SynchronousOnly` | `input` | Final child text |
| `BackgroundOnly` | `input` | Background launch receipt |
| `ModelChoice` | `input`, `invocationMode` | Final child text or background launch receipt |

For `ModelChoice`, the model can pass:

```json
{
  "input": "Review this draft.",
  "invocationMode": "background"
}
```

Background subagent calls return immediately with a structured receipt that includes the background `taskId`. The child result is delivered later through the background task notification system, using the child text as the completion summary.

See [Background Tasks And Notifications](../../concepts/background-tasks-and-notifications.md).

## Choose The Source

A subagent has three configuration sources. Every source still requires a stable `AgentId`:

| Source | Use When | What Happens |
| --- | --- | --- |
| `SubAgent.FromConfig(...)` | The specialist is defined next to the parent harness. | HPD builds a child agent from the inline `AgentConfig`. If the child config does not specify its own chat provider, it can inherit the parent chat client. |
| `SubAgent.FromParent(...)` | The specialist should use the effective parent configuration. | HPD stores an immutable serializable snapshot of the parent configuration as the child definition. Child harnesses can still extend it. |
| `SubAgent.FromAgentId(...)` | The specialist is stored, managed, or reused outside this harness. | HPD loads the stored agent definition through the parent agent store, then runs it with the selected context, availability, and run-configuration policies. |

Registering the parent tool harness materializes its supplied and parent-derived child definitions into `IAgentStore`. The definition records its owning harness/member and configuration fingerprint. Reusing one `AgentId` with a different owner or definition fails instead of silently replacing the agent. Inline config is best for local specialists that belong to one product surface. Stored agents are better when specialists are user-created, versioned, administered, or shared across parent agents.

## Add Child Tools

Pass tool harness types after the context policy:

```csharp
[SubAgent]
public SubAgent Researcher() =>
    SubAgent.FromConfig(
        "specialist.researcher",
        "researcher",
        "Researches a question using the approved search tools.",
        new AgentConfig
        {
            Name = "Researcher",
            SystemInstructions = "Research the request and return cited notes.",
        },
        contextPolicy: SubAgentContextPolicy.Handoff,
        toolharnessTypes: [typeof(SearchTools), typeof(CitationTools)]);
```

Those harnesses are registered on the child agent, not exposed directly to the parent. The parent sees only the `researcher` tool.

## Choose The Context Policy

The context policy directly decides how the child conversation begins.

| Policy | Session | History | Use When |
| --- | --- | --- | --- |
| `SubAgentContextPolicy.Handoff` | Parent session | Parent conversation rendered as text | Default. The child needs the current conversation. |
| `SubAgentContextPolicy.Fresh` | Parent session | Starts empty | The delegated input is self-contained. |
| `SubAgentContextPolicy.Isolated` | New session | Starts empty | The child should be isolated from the parent session. |
| `SubAgentContextPolicy.ModelChoice` | Parent session unless isolated | Model chooses `handoff`, `fresh`, or `isolated` | Different calls need different amounts of context. |

When `ModelChoice` is enabled, the tool schema exposes
`context: "handoff" | "fresh" | "isolated"`. Omitting it defaults to `fresh`.
Fixed policies do not expose the argument.

## Continue A Child Conversation

Completing a delegation does not destroy the child conversation. Persist these three values from the returned thread or thread graph:

```text
defaultAgentId + sessionId + threadId
```

Use that scope to reopen the child with its default agent or submit another message through the thread APIs. Subagent tool invocations always create a new child thread; `invocationId` identifies one parent-to-child delegation and is not a reusable routing identity.

## Thread Metadata

Subagent-created threads are hidden by default and include metadata that links them back to the parent:

- `kind = subagent`
- `defaultAgentId`
- `subAgentName`
- `subAgentSourceKind`
- `parentSessionId`
- `parentThreadId`
- `parentToolCallId`
- `invocationId`
- `contextPolicy`
- `visibility = hidden`
- `createdBy = subagent`

You can add custom thread metadata when creating the subagent:

```csharp
SubAgent.FromConfig(
    "specialist.reviewer",
    "reviewer",
    "Reviews the draft.",
    reviewerConfig,
    contextPolicy: SubAgentContextPolicy.Handoff,
    metadata: new Dictionary<string, object>
    {
        ["team"] = "writing",
        ["priority"] = "normal"
    });
```

Use metadata for UI grouping, retention policy, audit labels, or application-specific routing. Do not depend on thread names or invocation ids for identity. Use `sessionId + threadId` for the conversation and choose an `agentId` for each run; `defaultAgentId` supplies the normal continuation choice.

## Events And Hierarchy

Subagent runs are event-native. The parent journal records `SubAgentInvocationStartedEvent` followed by `SubAgentInvocationCompletedEvent`, `SubAgentInvocationFailedEvent`, or `SubAgentInvocationCancelledEvent`. These events correlate through `invocationId` and identify the child agent, session, thread, resolved context policy, and invocation mode. The parent also emits normal tool-call events, while the child owns its normal run and message lifecycle in the child journal.

`AddCodingHarnessTui()` projects coding subagent tools such as `explore`, `worker`, and `reviewer` into one semantic parent-transcript entry. The entry moves from preparing to running and then to completed, failed, or cancelled as the durable lifecycle events arrive. Prompt, result, and error text is bounded in the parent transcript; use `/subagents` to open the complete child conversation. Replaying the same parent events reconstructs the same presentation after hydration.

That lets a host render:

- the parent model deciding to call `researcher`
- `ToolCallStart`, `ToolCallArgs`, `ToolCallEnd`, and `ToolCallResult` for the subagent tool
- child text, tool calls, permissions, custom events, and turn events
- the parent model continuing after the child result

The parent runtime state includes answerable requests originating from descendant child threads. Post the response through the active parent coordinator route while preserving the request id and child `sessionId` and `threadId` on the response event. The thread graph exposes runtime children with their `defaultAgentId`, so a client can reconstruct the normal child runtime without preventing another explicitly selected agent from using the thread.

For rendering patterns, see [Event Streams And Hierarchies](../../concepts/event-streams-and-hierarchies.md), [Workflow Events](../multi-agent/workflow-events.md), and [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md).

## Handoff Compaction

For `Handoff` subagents, configure the optional textual handoff compaction on
the direct child run:

```csharp
var childRun = new SubAgentRunConfig
{
    HandoffCompaction = new CompactionSpecification
    {
        Point = new CompactAtCurrentHead(),
        Preservation = new PreservePreviousTurns(3),
        Strategy = new SummarizingCompaction(),
        CommitMode = CompactionCommitMode.Hard
    }
};
```

The compaction runs before the child receives the parent text handoff. Set
`SubAgentRunDefaults.HandoffCompaction` when descendants should receive the
same policy. The child run configuration and execution policy are captured
before invocation, so later caller mutation does not change an admitted child.

See [Compaction](../sessions-and-streaming/compaction.md).

## Permissions

Subagent tools require permission by default because they can run another agent, call child tools, and write durable history. Treat permission prompts as a parent-level decision: the parent is asking to delegate work to the named specialist.

If you build a custom permission middleware, you can approve at whatever level your product needs: the subagent tool call, the generated input, the child's own tools, or a higher-level command signature stored in middleware state.

See [Permissions](../middleware/permissions.md) and [Bidirectional Events](../events/bidirectional-events.md).

## Subagents Vs Workflows

Use subagents when:

- the parent model should decide when to delegate
- each specialist is naturally a tool
- the parent should receive a single tool result and continue
- thread policy is more important than graph topology

Use multi-agent workflows when:

- the app should control the graph
- routing, fan-out, review gates, or fixed stages matter
- node outputs need explicit keys
- you want to run the workflow directly from app code or expose the whole graph through `[MultiAgent]`

See [Multi-Agent Overview](../multi-agent/overview.md) and [Multi-Agent Capabilities](../tools/multi-agent-capabilities.md).

## Boundaries

`[SubAgent]` is the normal authoring path. The containing harness must use generated registration; harness registration no longer falls back to runtime reflection.

Generated and reflection-created subagent wrappers freeze the same declaration and materialize the same durable definition. Both use the configured runtime resolver, session store, agent store, event coordination, and hierarchical metadata. Treat low-level runtime helpers as implementation details unless you are extending HPD Agent itself.
