# Execution Model

Multi-agent workflows execute named agent nodes through explicit routes. The runtime decides which nodes are ready, runs ready nodes by layer, records node outputs, evaluates routes, and emits workflow events.

## Nodes

A node is a named HPD agent plus node options.

```csharp
.AddAgent("research", researchConfig, node => node
    .WithOutputKey("research")
    .WithTimeout(TimeSpan.FromSeconds(45)))
```

The node id is the workflow identity for routing and events. The agent name is the child agent identity used inside child-agent events.

A node can be backed by:

- an `AgentConfig`
- a prebuilt `Agent`
- an inline `AgentBuilder` configuration

Config-backed and inline-built agents are created when the workflow executes. If a child agent does not configure its own provider, it can inherit the parent chat client passed to `ExecuteStreamingAsync(...)`.

## Boundary Nodes

Workflows have implicit `START` and `END` boundaries.

If a node has no declared incoming edge, it is an entry node. If a node has no declared outgoing edge, it is an exit node.

```text
START -> entry nodes
exit nodes -> END
```

You can still declare `START` and `END` explicitly when the entry or exit should be visible in the workflow definition:

```csharp
.From("START").To("classify")
.From("final").To("END")
```

## Edges

Edges decide what can run after a node completes.

```csharp
.From("draft").To("review")
```

An edge can route to one node or several nodes:

```csharp
.From("research").To("summarize", "fact_check")
```

Several source nodes can also route to the same target:

```csharp
.From("summarize", "fact_check").To("write")
```

Use explicit edges whenever order matters. Agent insertion order is not execution order.

## Layers

Nodes that are ready at the same time can run in the same layer. A workflow that fans out from one node to two downstream nodes can produce layer events like this:

```text
Layer 0: research
Layer 1: summarize, fact_check
Layer 2: write
```

Subscribe to `WorkflowLayerStartedEvent` and `WorkflowLayerCompletedEvent` when the UI needs to show parallel progress. Subscribe to `WorkflowAgentStartedEvent` and `WorkflowAgentCompletedEvent` when the UI needs per-node status.

## Inputs And Outputs

Each completed node writes an output dictionary. The most common shape is one string response stored under the node's output key:

```csharp
.AddAgent("draft", draftConfig, node => node.WithOutputKey("draft"))
.AddAgent("review", reviewConfig, node => node.WithInputKey("draft"))
```

Input resolution is documented in [Data Flow Between Nodes](data-flow-between-nodes.md). The important rule is to name the contract between nodes. Do not rely on downstream agents magically seeing the right upstream text.

## Conditions

Route conditions read the source node's outputs.

```csharp
.From("classify").To("billing").WhenEquals("intent", "billing")
.From("classify").To("technical").WhenEquals("intent", "technical")
```

Declarative conditions can be exported and loaded from config:

```csharp
using static HPD.MultiAgent.Routing.Condition;

.From("triage").To("vip_billing").When(And(
    Equals("intent", "billing"),
    Equals("tier", "VIP")))
```

Predicate conditions are in-process only:

```csharp
.From("classify").To("review").When(ctx =>
    ctx.Outputs.TryGetValue("needs_review", out var value) &&
    value is true)
```

Use declarative conditions when a workflow must round-trip through JSON config. Use predicates when the workflow only runs from code and needs custom logic.

## Handoffs

A router node can choose a downstream node by calling a generated handoff tool.

```csharp
AgentWorkflow.Create()
    .AddRouterAgent("router", routerConfig)
        .WithHandoff("math", "Use for calculation-heavy requests.")
        .WithHandoff("research", "Use for research-heavy requests.")
    .AddAgent("math", mathConfig)
    .AddAgent("research", researchConfig)
    .BuildAsync();
```

At runtime, HPD gives the router tools such as `handoff_to_math` and `handoff_to_research`. The selected target is written to `handoff_target`, and route conditions use that output.

## Errors And Skips

Node options include error-mode helpers:

```csharp
.AddAgent("review", reviewConfig, node => node.OnErrorSkip())
```

Workflow events expose the public outcome:

- `WorkflowAgentCompletedEvent.Success`
- `WorkflowAgentCompletedEvent.ErrorMessage`
- `WorkflowAgentSkippedEvent`
- `WorkflowCompletedEvent.SuccessfulNodes`
- `WorkflowCompletedEvent.FailedNodes`
- `WorkflowCompletedEvent.SkippedNodes`

Validate error policy in the workflow shape you are using before treating an error mode as a product contract.

## Loops And Limits

Cyclic workflows should set an iteration limit:

```csharp
.WithMaxIterations(5)
```

Use loops for bounded refinement, validation, or retry-style workflows where the route condition eventually stops traversing the loop.

## Checkpointing

Checkpointing is opt-in:

```csharp
.WithCheckpointing()
.WithJsonWorkflowStore("App_Data/workflows")
```

Checkpoint storage is a workflow execution feature. It does not add a separate public checkpoint event family to the multi-agent event stream.

See [Checkpointing](checkpointing.md).

## Related Pages

- [Build A Multi-Agent Workflow](build-a-workflow.md)
- [Data Flow Between Nodes](data-flow-between-nodes.md)
- [Routing And Handoffs](routing-and-handoffs.md)
- [Workflow Events](workflow-events.md)
