# Checkpointing

In HPD Agent 0.9.5, checkpointing and restart recovery are owned by HPD Base durable
activations. The in-process `AgentWorkflowInstance` executes a graph but does not expose
`WithCheckpointing` or an application-owned workflow checkpoint store.

## Execution Boundary

Use direct workflow execution when the process can own the complete run:

```csharp
var workflow = await AgentWorkflow.FromFile("review-workflow.yaml").BuildAsync();
var result = await workflow.RunAsync("Review the supplied evidence.");
```

Direct execution is useful for tests, local tools, and request-scoped orchestration. It
does not promise resume after process termination.

Use a durable HPD Base activation when the run must survive worker restart, retry, or
deferred scheduling:

```csharp
using HPD.Agent.MultiAgent.HpdBase;
using HPD.Base;
using HPD.Base.AspNetCore;
using HPD.Graph.Abstractions.Config;
using HPD.Graph.Base;

BaseGraphActivationDefinition definition =
    BaseGraphActivationRegistration.Create(
        graphConfig,
        definitionVersion: 1,
        grants,
        limits,
        sourceGrantIds);

builder.Services.AddHPDBase(baseBuilder => baseBuilder
    .AddAspNetCore()
    .AddGraphPersistence()
    .AddGraphActivation(definition));

builder.Services.AddHPDBaseActivationWorkers();
builder.Services.AddHPDMultiAgentHpdBase(definition);
```

The same sealed `BaseGraphActivationDefinition` must be passed to the Base activation
registration and to the multi-agent API adapter.

## State Ownership

Keep these state paths separate:

| State | Owner |
| --- | --- |
| graph execution and checkpoints | HPD Graph through HPD Base activations |
| activation attempts, retries, and recovery | HPD Base |
| node agent transcripts | HPD session and thread stores |
| workflow configuration and exports | `HPD-Agent.MultiAgent` |

Conversation policies configure the session and thread path. They do not make direct
workflow execution restart-durable. See [Conversation Policies](conversation-policies.md).

## Events

HPD Graph and HPD Base own durable execution inspection. The multi-agent workflow
projection does not emit a separate `CheckpointSaved` event.

Application code can continue to consume the public live workflow events:

- `WorkflowStartedEvent`
- `WorkflowAgentStartedEvent`
- `WorkflowAgentCompletedEvent`
- `WorkflowAgentSkippedEvent`
- `WorkflowApprovalRequestedEvent`
- `WorkflowApprovalTimedOutEvent`
- `WorkflowEdgeTraversedEvent`
- `WorkflowLayerStartedEvent`
- `WorkflowLayerCompletedEvent`
- `WorkflowCompletedEvent`
- `WorkflowDiagnosticEvent`
- `WorkflowRecoveryEvent`
- `WorkflowAttemptCompletedEvent`

See [Workflow Events](workflow-events.md) and
[ASP.NET Multi-Agent Workflows](../hosting/multiagent-aspnet.md).

## Related Pages

- [Build A Multi-Agent Workflow](build-a-workflow.md)
- [Conversation Policies](conversation-policies.md)
- [Config And Export](config-and-export.md)
- [Workflow Events](workflow-events.md)
