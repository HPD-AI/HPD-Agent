# Build A Multi-Agent Workflow

This page shows the normal construction path for workflow graphs. For model-callable workflow tools, see [Multi-Agent Capabilities](../tools/multi-agent-capabilities.md).

## Add Agents

A workflow can contain config-backed agents, prebuilt agents, or agents configured inline.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("ResearchAndWrite")
    .AddAgent("research", new AgentConfig
    {
        Name = "Researcher",
        SystemInstructions = "Collect the key facts needed to answer the request.",
    }, node => node.WithOutputKey("research"))
    .AddAgent("write", new AgentConfig
    {
        Name = "Writer",
        SystemInstructions = "Write a concise final answer using the provided research.",
    }, node => node.WithInputKey("research"))
    .From("research").To("write")
    .BuildAsync();
```

`AgentWorkflow.Create()` returns the fluent builder. `BuildAsync()` creates an `AgentWorkflowInstance`, which is the runnable workflow object used by `RunAsync(...)`, `ExecuteStreamingAsync(...)`, subscriptions, and config export.

Agent insertion order is not the execution order. Declare edges for the order you want. A workflow with two unrelated agents and no declared edge has two entry nodes, not an automatic `first -> second` chain.

The example above constructs the graph. Direct execution still needs each config-backed node to resolve a provider client. For a standalone workflow, either configure providers on those agents through the host's provider composition or construct prebuilt agents with `AgentBuilder.WithChatClient(...)`. Parent-client inheritance applies when the workflow is invoked from an agent through `[MultiAgent]`.

## Construction Forms

Use `AgentConfig` when the workflow should build the child agent at execution time:

```csharp
.AddAgent("review", new AgentConfig
{
    Name = "Reviewer",
    SystemInstructions = "Review the answer for accuracy.",
})
```

Use a prebuilt `Agent` when the child has already been constructed:

```csharp
.AddAgent("review", reviewAgent)
```

Use inline builder configuration when the node needs local builder customization:

```csharp
.AddAgent("review", builder =>
{
    builder.WithInstructions("Review the answer for accuracy.");
    builder.WithToolHarness<ReviewTools>();
})
```

Config-backed and inline-built agents are created lazily when the workflow executes. If they do not configure their own provider, they can inherit the invoking parent agent's client when the workflow is called through `[MultiAgent]`. Direct workflow execution has no parent agent from which to inherit.

## Conversation Policy

Workflow outputs are separate from durable conversation history. Add a conversation policy when each node agent's transcript should be saved into HPD sessions and threads:

```csharp
using HPD.Agent.Serialization;

if (!AgentEventCompositionHost.TryGetDefaultApplication(out var events))
    throw new InvalidOperationException("A generated application event composition is required.");

var sessionStore = new FileSessionStore("App_Data/sessions", events.Codec);

var workflow = await AgentWorkflow.Create()
    .WithName("ResearchAndWrite")
    .WithSessionStore(sessionStore)
    .WithConversation(MultiAgentConversationPolicies.ForkThreadPerAgent())
    .AddAgent("research", researchConfig, node => node.WithOutputKey("research"))
    .AddAgent("write", writeConfig, node => node.WithInputKey("research"))
    .From("research").To("write")
    .BuildAsync();
```

`WithSessionStore(...)` stores conversations and threads. The store must use the application's generated event codec. If nodes are prebuilt, configure those agents with the exact same store.

Use `SharedWorkflowThread()` for one transcript, `ThreadPerAgent()` for durable agent-local threads, or `ForkThreadPerAgent()` when agents should start from the same request and write into separate threads.

## Edges

Edges define the graph:

```csharp
.From("research").To("write")
```

Multiple sources and targets are allowed:

```csharp
.From("draft", "facts").To("review")
```

If you do not declare a node's incoming edge, HPD adds an edge from `START`. If you do not declare a node's outgoing edge, HPD adds an edge to `END`.

```text
START -> first node without incoming declared edge
last node without outgoing declared edge -> END
```

You can still declare `START` and `END` yourself when you want the entry and exit points to be explicit:

```csharp
.From("START").To("classify")
.From("write").To("END")
```

Use `.From(...).To(...)` for sequencing whenever one agent must run after another.

## Execute

Subscribe before execution if the application needs live events:

```csharp
using var events = workflow.SubscribeAny(evt =>
{
    timeline.Project(evt);
});

await foreach (var evt in workflow.ExecuteStreamingAsync(
    "Compare the two implementation options.",
    cancellationToken: CancellationToken.None))
{
    // Render from this stream, or from subscriptions, but avoid double-rendering both.
}
```

`ExecuteStreamingAsync(...)` emits workflow graph events and child agent events. `RunAsync(...)` drains the workflow and returns a `WorkflowResult`.

The direct overloads accept an optional parent `IEventCoordinator` or `AgentMetadata`. When the workflow is generated as a `[MultiAgent]` tool, the wrapper supplies this parent context internally.

## Runtime Options

Node options control input, output, structured result handling, retry, timeout, context, approvals, and handoff targets.

```csharp
.AddAgent("analyze", analyzerConfig, node => node
    .WithInputKey("research")
    .WithOutputKey("analysis")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithRetry(maxAttempts: 3)
    .WithInstructions("Prefer concrete evidence over speculation."))
```

`WithTimeout(...)` is applied to the graph node and the child agent run. `WithRetry(...)` is wired through the graph node retry policy.

Error-mode helpers such as `OnErrorFallback(...)` are part of the configuration/export surface, but validate execution behavior in your workflow before relying on them as runtime policy.

## Durable Host Execution

`AgentWorkflowInstance` is an in-process graph runtime. It does not own restart-durable checkpoint or resume state.

For durable, restart-safe execution, install the graph as an HPD Base activation and expose it through the multi-agent ASP.NET projection. HPD Base owns activation state, retries, checkpointing, and worker recovery. See [Checkpointing](checkpointing.md) and [ASP.NET Multi-Agent Workflows](../hosting/multiagent-aspnet.md).

## Related Pages

- [Choose A Composition Pattern](choose-a-pattern.md)
- [Execution Model](execution-model.md)
- [Workflow Patterns](workflow-patterns.md)
- [Data Flow Between Nodes](data-flow-between-nodes.md)
- [Routing And Handoffs](routing-and-handoffs.md)
- [Conversation Policies](conversation-policies.md)
- [Checkpointing](checkpointing.md)
- [Workflow Events](workflow-events.md)
