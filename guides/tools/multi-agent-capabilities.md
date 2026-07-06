# Multi-Agent Capabilities

`[MultiAgent]` lets a tool harness expose a workflow as a model-callable capability. The parent agent sees a normal tool. When the tool runs synchronously, HPD executes the workflow and can stream workflow and child-agent events through the parent event path.

Use this page when a parent agent should decide whether to run a workflow. Use [Build A Multi-Agent Workflow](../multi-agent/build-a-workflow.md) when application code should run the workflow directly.

## Author The Workflow Tool

The annotated method must take no parameters and return `AgentWorkflowInstance` or `Task<AgentWorkflowInstance>`.

```csharp
using HPD.Agent;
using HPD.MultiAgent;

public sealed class WorkflowTools
{
    [MultiAgent("Runs a draft and review workflow.", Name = "draft_and_review")]
    public Task<AgentWorkflowInstance> DraftAndReview() =>
        AgentWorkflow.Create()
            .WithName("DraftAndReview")
            .AddAgent("draft", new AgentConfig
            {
                Name = "Drafter",
                SystemInstructions = "Draft a concise answer.",
            }, node => node.WithOutputKey("draft"))
            .AddAgent("review", new AgentConfig
            {
                Name = "Reviewer",
                SystemInstructions = "Review and improve the draft.",
            }, node => node.WithInputKey("draft"))
            .From("draft").To("review")
            .BuildAsync();
}
```

Register the harness like any other tool harness:

```csharp
var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithToolHarness<WorkflowTools>()
    .BuildAsync();
```

## Generated Tool Shape

By default, the generated tool takes one model-facing argument:

```json
{
  "input": "Write and review a short answer."
}
```

If `input` is empty, the generated wrapper can fall back to the last user message. The wrapper passes the parent chat client into workflow execution when child agents do not configure their own provider.

## Invocation Mode

`[MultiAgent]` capabilities are synchronous by default: the parent calls the workflow tool, HPD waits for completion, and the tool result is the final workflow text.

Use `InvocationModePolicy` when the workflow can run independently:

```csharp
[MultiAgent(
    "Runs a draft and review workflow.",
    Name = "draft_and_review",
    InvocationModePolicy = AgentInvocationModePolicy.ModelChoice)]
public Task<AgentWorkflowInstance> DraftAndReview() => ...
```

Policies:

| Policy | Tool Shape | Tool Result |
| --- | --- | --- |
| `SynchronousOnly` | `input` | Final workflow text |
| `BackgroundOnly` | `input` | Background launch receipt |
| `ModelChoice` | `input`, `invocationMode` | Final workflow text or background launch receipt |

For background calls, the workflow starts as runtime-owned background work. The immediate tool result is a structured receipt with the background `taskId`; the final workflow text is delivered later through background task notifications.

The generated function metadata marks the capability as multi-agent:

- `CapabilityType = MultiAgent`
- `IsMultiAgent = true`
- `IsContainer = false`
- `ParentToolHarness = ...`
- `StreamEvents = ...`
- `TimeoutSeconds = ...`
- `InvocationModePolicy = ...`

The tool requires permission by default.

## Streaming Behavior

`StreamEvents` defaults to `true`:

```csharp
[MultiAgent("Runs a draft and review workflow.", StreamEvents = true)]
```

In the streaming path, the generated wrapper calls `workflow.ExecuteStreamingAsync(...)` with the parent event coordinator, parent metadata, and parent chat client. The parent stream can contain:

- parent tool call events for the `[MultiAgent]` function
- workflow events such as `WorkflowStartedEvent`
- child agent events emitted inside workflow nodes
- workflow completion events

Render the parent tool call and nested workflow as related projections of the same live stream. See [Workflow Events](../multi-agent/workflow-events.md).

If `StreamEvents = false`, the generated wrapper calls `RunAsync(...)` and returns the final result. That mode does not provide the same live parent workflow projection.

## Timeout Metadata

`TimeoutSeconds` is available on the attribute and appears in generated metadata:

```csharp
[MultiAgent("Runs a workflow.", TimeoutSeconds = 120)]
```

Validate enforcement in your target runtime before documenting this as an execution timeout policy. Use workflow or node timeout options when the workflow itself needs runtime timeout behavior.

## Boundary

`[MultiAgent]` belongs on a tool-harness method that produces a workflow. It is not a collapsed container. It is a model-callable workflow capability.

Do not combine `[MultiAgent]` with other capability attributes on the same method. Give the attribute a clear description so the model knows when to invoke the workflow.

## Related Pages

- [Build A Multi-Agent Workflow](../multi-agent/build-a-workflow.md)
- [Data Flow Between Nodes](../multi-agent/data-flow-between-nodes.md)
- [Routing And Handoffs](../multi-agent/routing-and-handoffs.md)
- [Workflow Events](../multi-agent/workflow-events.md)
