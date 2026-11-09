# Multi-Agent Overview

Multi-agent workflows coordinate multiple HPD agents through explicit stages and routes. Use them when a task naturally moves through roles, decisions, routing, review, validation, or parallel threads.

[Subagents](../agents/subagents.md) are tools exposed directly on one parent agent. Multi-agent workflows are different: the application defines the workflow shape, each node is an agent, and edges decide which node runs next.

## Mental Model

A workflow has five moving parts:

- nodes: named HPD agents
- edges: routes between nodes
- outputs: dictionaries produced by completed nodes
- events: workflow events plus normal child-agent events
- conversation policy: optional session and thread routing for durable node transcripts

Workflows have implicit `START` and `END` boundary nodes. At run time, each agent node receives workflow input, runs an HPD `Agent`, writes outputs, and lets downstream edges or nodes consume those outputs.

```text
START
  -> classify
       -> math_solver
       -> researcher
       -> writer
  -> END
```

## Package And Namespace

```csharp
using HPD.Agent;
using HPD.Agent.Serialization;
using HPD.MultiAgent;
```

## Smallest Workflow

```csharp
await using var drafter = await new AgentBuilder(new AgentConfig
{
    Name = "Drafter",
    SystemInstructions = "Draft a concise answer.",
})
    .WithChatClient(chatClient)
    .BuildAsync();

await using var reviewer = await new AgentBuilder(new AgentConfig
{
    Name = "Reviewer",
    SystemInstructions = "Improve the draft for clarity and correctness.",
})
    .WithChatClient(chatClient)
    .BuildAsync();

var workflow = await AgentWorkflow.Create()
    .WithName("DraftAndReview")
    .AddAgent("draft", drafter, node => node.WithOutputKey("draft"))
    .AddAgent("review", reviewer, node => node.WithInputKey("draft"))
    .From("draft").To("review")
    .BuildAsync();
```

`BuildAsync()` creates a runnable workflow instance. A config-backed node can be built lazily, but direct `RunAsync(...)` and `ExecuteStreamingAsync(...)` execution still needs that node to resolve a provider client. When a workflow is exposed through `[MultiAgent]`, a child without its own provider can inherit the invoking parent agent's client.

By default, workflow data flows through node outputs and live events. Add a conversation policy when node agent transcripts should be written into HPD sessions and threads:

```csharp
if (!AgentEventCompositionHost.TryGetDefaultApplication(out var events))
    throw new InvalidOperationException("A generated application event composition is required.");

var sessionStore = new FileSessionStore("App_Data/sessions", events.Codec);

var workflow = await AgentWorkflow.Create()
    .WithName("DraftAndReview")
    .WithSessionStore(sessionStore)
    .WithConversation(MultiAgentConversationPolicies.ForkThreadPerAgent())
    .AddAgent("draft", draftConfig)
    .AddAgent("review", reviewConfig)
    .From("draft").To("review")
    .BuildAsync();
```

The session store must use the host's generated event codec. Prebuilt node agents must use that exact same store. Durable graph execution and restart recovery are owned by HPD Base activations rather than by the session store.

## Run And Observe

Use `ExecuteStreamingAsync(...)` when you want live workflow and child-agent events:

```csharp
using var subscription = workflow.SubscribeAny(evt =>
{
    Console.WriteLine(evt.GetType().Name);
});

await foreach (var _ in workflow.ExecuteStreamingAsync(
    "Write a short release note.",
    cancellationToken: CancellationToken.None))
{
    // Drain the stream while the subscription observes events.
}
```

Use `RunAsync(...)` when you only need the final workflow result.

When the workflow is called through `[MultiAgent]`, the generated wrapper supplies parent event and client context automatically. It does not use a separate `parentChatClient` execution overload.

## Common Patterns

Multi-agent workflows commonly take these shapes:

- sequential pipeline: one role hands work to the next role
- parallel fan-out: several roles work from the same upstream result
- fan-in synthesis: one role combines several upstream outputs
- field router: structured output chooses the next route
- router handoff: a router agent calls a generated handoff tool
- review gate: a reviewer approves, rejects, or sends work back
- bounded validator loop: a validator routes back to a reviser until a limit is reached

See [Workflow Patterns](workflow-patterns.md).

## When To Use Multi-Agent

Use multi-agent workflows for:

- role pipelines, such as draft -> review -> final
- routers that pick a specialized downstream agent
- structured classifiers that route by output fields
- review gates and approval stages
- workflows exposed as parent-agent tools through `[MultiAgent]`

Use a normal tool harness when a single agent can call one capability directly. Use [subagents](../agents/subagents.md) when a parent agent should delegate to child agents as ordinary tools rather than execute an explicit workflow graph.

## Read Next

- [Build A Multi-Agent Workflow](build-a-workflow.md)
- [Choose A Composition Pattern](choose-a-pattern.md)
- [Execution Model](execution-model.md)
- [Workflow Patterns](workflow-patterns.md)
- [Conversation Policies](conversation-policies.md)
- [Data Flow Between Nodes](data-flow-between-nodes.md)
- [Routing And Handoffs](routing-and-handoffs.md)
- [Checkpointing](checkpointing.md)
- [Workflow Events](workflow-events.md)
- [Multi-Agent Capabilities](../tools/multi-agent-capabilities.md)
- [ASP.NET Multi-Agent Workflows](../hosting/multiagent-aspnet.md)
