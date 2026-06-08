# Conversation Policies

Multi-agent workflows have two separate state paths:

- workflow state: node execution, node outputs, routing, approvals, and checkpoints
- conversation state: HPD sessions and branches that store agent transcripts

Workflow storage is configured with `WithInMemoryWorkflowStore(...)` or `WithJsonWorkflowStore(...)`. Conversation storage is configured with `WithSessionStore(...)`. They are intentionally separate.

Conversation policy controls where node agent transcripts are written. It does not control node output routing, edge traversal, approvals, checkpointing, or the data passed between workflow nodes.

## Default

By default, a workflow runs without durable conversation routing:

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("DraftAndReview")
    .AddAgent("draft", draftConfig)
    .AddAgent("review", reviewConfig)
    .From("draft").To("review")
    .BuildAsync();
```

Node outputs still flow through the workflow, and workflow events still stream live. The child agent turns are not assigned a durable `SessionId` and `BranchId`.

## Shared Workflow Branch

Use one branch when the workflow should read like a single collaborative transcript:

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("DraftAndReview")
    .WithSessionStore(new JsonSessionStore("App_Data/sessions"))
    .WithConversation(MultiAgentConversationPolicies.SharedWorkflowBranch())
    .AddAgent("draft", draftConfig)
    .AddAgent("review", reviewConfig)
    .From("draft").To("review")
    .BuildAsync();
```

Every node agent writes to the same session and branch.

When multiple nodes target the same branch at the same time, the workflow serializes those branch writes. This keeps branch snapshots, branch middleware state, and appended branch events from overwriting each other. Branches created by `BranchPerAgent` and `ForkBranchPerAgent` are independent, so different node branches can still run in parallel.

## Branch Per Agent

Use one branch per node when each agent needs its own durable workspace:

```csharp
.WithSessionStore(new JsonSessionStore("App_Data/sessions"))
.WithConversation(MultiAgentConversationPolicies.BranchPerAgent())
```

The workflow creates one session for the execution and a stable branch for each node agent in that run.

## Fork Branch Per Agent

Use forked branches when each agent should see the same starting request but write separately:

```csharp
.WithSessionStore(new JsonSessionStore("App_Data/sessions"))
.WithConversation(MultiAgentConversationPolicies.ForkBranchPerAgent())
```

The workflow creates a root branch with the original input, then forks one child branch per node agent. This is useful for review, comparison, fan-out, and audit-heavy workflows because each agent leaves behind an inspectable transcript.

## Existing Session

Pass a session id when the workflow should attach to an existing user or case:

```csharp
.WithSessionStore(sessionStore)
.WithConversation(MultiAgentConversationPolicies.ForkBranchPerAgent(
    sessionId: "support-case-123"))
```

The workflow still creates branches according to the selected policy, but those branches live inside the supplied session.

## Store Rules

Conversation policies other than `None` require `WithSessionStore(...)`.

Config-backed and inline-built node agents receive the workflow session store automatically. Prebuilt agents must already use the same session store, otherwise the workflow fails early. This prevents one workflow run from scattering branches across multiple stores.

## Choosing A Policy

Use `SharedWorkflowBranch` for a single transcript.

Use `BranchPerAgent` for durable agent-local workspaces.

Use `ForkBranchPerAgent` when several agents should start from the same request and produce separate inspectable branches.

Use no conversation policy for short-lived orchestration where workflow outputs are enough.
