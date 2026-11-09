# ASP.NET Multi-Agent Workflows

`HPD-Agent.MultiAgent.AspNetCore` projects installed multi-agent graph definitions into an ASP.NET
Core surface for workflow discovery and durable run activation.

This is separate from the single-agent hosted runtime. A normal host profile uses sessions,
threads, inputs, and SSE. A multi-agent graph run uses HPD.Base durable activations.

## Current Surface

`MapHPDMultiAgentApi(...)` currently exposes:

```text
GET  /multi-agent/workflows
POST /multi-agent/workflows/{workflowId}/runs
```

The default prefix is `/multi-agent`. Pass a prefix to change it:

```csharp
app.MapHPDMultiAgentApi("/api/multi-agent");
```

This section covers only the projection implemented by the current package. It does not provide
workflow-detail, run-history, run-status, event-stream, or approval routes under
`/multi-agent/workflows`.

## Requirements

A multi-agent API route needs three separate pieces:

1. A `BaseGraphActivationDefinition` containing the installed workflow graph identity and input
   authority.
2. HPD.Base persistence and activation workers capable of executing that definition.
3. `AddHPDMultiAgentHpdBase(...)` so the API adapter can discover and enqueue the same definition.

The graph definition is trusted host configuration. Use HPD-Graph and HPD.Base setup to create a
`BaseGraphActivationDefinition` with the required activation grants, execution limits, source grant
ids, and worker authority.

The host wiring has this shape:

```csharp
using HPD.Agent.MultiAgent.AspNetCore;
using HPD.Agent.MultiAgent.HpdBase;
using HPD.Base;
using HPD.Base.AspNetCore;
using HPD.Graph.Base;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Created from the trusted workflow graph through HPD-Graph activation setup.
BaseGraphActivationDefinition workflowDefinition = CreateInstalledWorkflow();

builder.Services.AddHPDBase(baseBuilder => baseBuilder
    .AddAspNetCore()
    .AddGraphPersistence()
    .AddGraphActivation(workflowDefinition));

builder.Services.AddHPDBaseActivationWorkers();
builder.Services.AddHPDMultiAgentHpdBase(workflowDefinition);

var app = builder.Build();

app.MapHPDMultiAgentApi("/api/multi-agent");

app.Run();
```

`CreateInstalledWorkflow()` is application setup, not a helper supplied by this package. The host
must retain the same sealed definition for both the API projection and the Base activation worker.

`AddHPDMultiAgentHpdBase(...)` registers the HPD Base-backed
`IHPDMultiAgentWorkflowBackend`. It requires at least one uniquely identified installed graph and
fails during service registration when no graph is supplied.

## Provider-Neutral Backend

`HPD-Agent.MultiAgent.AspNetCore` can also project another durable implementation. Register the
neutral services and one or more backends before mapping the routes:

```csharp
builder.Services.AddHPDMultiAgentAspNetCore();
builder.Services.AddSingleton<IHPDMultiAgentWorkflowBackend, MyWorkflowBackend>();

var app = builder.Build();
app.MapHPDMultiAgentApi();
```

The backend owns persistence and idempotency. `MapHPDMultiAgentApi()` fails at startup when no
backend is registered.

## Discover Workflows

```http
GET /api/multi-agent/workflows
```

The response contains workflow identities and immutable graph identity information:

```json
{
  "workflows": [
    {
      "workflowId": "evidence-review",
      "graphVersion": "1.0.0",
      "definitionVersion": 1,
      "graphChecksum": "..."
    }
  ]
}
```

Use `workflowId` in the activation route. It is the installed graph identity, not a stored
single-agent definition id.

## Start A Durable Run

```http
POST /api/multi-agent/workflows/evidence-review/runs
Content-Type: application/json
Idempotency-Key: evidence-review-request-001

{
  "executionId": "review-2026-11-08-001",
  "input": {
    "text": "Review the supplied evidence."
  }
}
```

`executionId` is optional. When omitted, hosting derives one from the canonical input and
idempotency key.

`DueAtUnixMilliseconds` can schedule the activation for a future time:

```json
{
  "input": {
    "text": "Run this workflow later."
  },
  "dueAtUnixMilliseconds": 1794201600000
}
```

The `Idempotency-Key` header is required and must appear exactly once. Reusing the key with the same
workflow and canonical input produces an idempotent replay; conflicting reuse is rejected.

Accepted requests return durable activation authority:

```json
{
  "executionId": "review-2026-11-08-001",
  "activationId": "...",
  "state": "...",
  "disposition": "..."
}
```

Acceptance means the run was committed to the durable activation system. It does not mean the graph
has finished, and this POST does not stream workflow events.

## Observing A Run

The current multi-agent projection does not expose a run status or event route under
`/multi-agent/workflows/{workflowId}/runs`. Do not assume that routes parallel to the single-agent
`/state` and `/events` surface exist.

Build run inspection and live observation on:

- HPD.Base activation inspection and administration routes with correctly scoped authority.
- HPD-Graph execution inspection surfaces for graph state and checkpoint data.
- Multi-agent workflow events when the application can route them into its own projection or live
  delivery layer.

Keep the following identities separate:

```text
workflowId       installed graph definition
executionId      logical graph execution
activationId     durable Base activation attempt chain
nodeId           one agent node inside the graph
agentId          the agent identity used inside that node
```

## Application UI Versus Workflow UI

Use a normal host profile plus stored agent definitions when the UI should show several independent
agent chats side by side. Each agent has its own `agentId + sessionId + threadId` runtime and SSE
stream.

Use the multi-agent activation surface when the application starts a graph run and the UI needs to
show workflow topology, layer progress, node status, and graph results. That UI must add run
inspection and event delivery over the durable Graph/Base infrastructure because the current
`HPD-Agent.MultiAgent.AspNetCore` package does not provide a complete run-dashboard API.

## Related Pages

- [Host Profiles And Agents](../../concepts/host-profiles-and-agents.md)
- [Multi-Agent Overview](../multi-agent/overview.md)
- [Workflow Events](../multi-agent/workflow-events.md)
- [ASP.NET Core Hosting](aspnet-core.md)
- [Hosted Endpoints Reference](../../reference/hosted-endpoints.md)
