# Conversation Policies

Multi-agent workflows have two separate state paths:

- workflow execution state: node execution, node outputs, routing, approvals, and Base activation state
- conversation state: HPD sessions and threads that store agent transcripts

Conversation storage is configured with `WithSessionStore(...)`. Durable workflow
execution and checkpoint persistence are configured separately through HPD Base. They are
intentionally separate.

Conversation policy controls where node agent transcripts are written. It does not control node output routing, edge traversal, approvals, checkpointing, or the data passed between workflow nodes.

## Session Store Setup

Create the conversation store from the application's generated event codec:

```csharp
using HPD.Agent.Serialization;

if (!AgentEventCompositionHost.TryGetDefaultApplication(out var events))
    throw new InvalidOperationException("A generated application event composition is required.");

var sessionStore = new FileSessionStore("App_Data/sessions", events.Codec);
```

Use the same store instance for the workflow and every prebuilt node agent.

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

Node outputs still flow through the workflow, and workflow events still stream live. The child agent turns are not assigned a durable `SessionId` and `ThreadId`.

## Shared Workflow Thread

Use one thread when the workflow should read like a single collaborative transcript:

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("DraftAndReview")
    .WithSessionStore(sessionStore)
    .WithConversation(MultiAgentConversationPolicies.SharedWorkflowThread())
    .AddAgent("draft", draftConfig)
    .AddAgent("review", reviewConfig)
    .From("draft").To("review")
    .BuildAsync();
```

Every node agent writes to the same session and thread.

When multiple nodes target the same thread at the same time, the workflow serializes those thread writes. This keeps thread snapshots, thread middleware state, and appended thread events from overwriting each other. Threads created by `ThreadPerAgent` and `ForkThreadPerAgent` are independent, so different node threads can still run in parallel.

## Thread Per Agent

Use one thread per node when each agent needs its own durable workspace:

```csharp
.WithSessionStore(sessionStore)
.WithConversation(MultiAgentConversationPolicies.ThreadPerAgent())
```

The workflow creates one session for the execution and a stable thread for each node agent in that run.

## Fork Thread Per Agent

Use forked threads when each agent should see the same starting request but write separately:

```csharp
.WithSessionStore(sessionStore)
.WithConversation(MultiAgentConversationPolicies.ForkThreadPerAgent())
```

The workflow creates a root thread with the original input, then forks one child thread per node agent. This is useful for review, comparison, fan-out, and audit-heavy workflows because each agent leaves behind an inspectable transcript.

## Existing Session

Pass a session id when the workflow should attach to an existing user or case:

```csharp
.WithSessionStore(sessionStore)
.WithConversation(MultiAgentConversationPolicies.ForkThreadPerAgent(
    sessionId: "support-case-123"))
```

The workflow still creates threads according to the selected policy, but those threads live inside the supplied session.

## Store Rules

Conversation policies other than `None` require `WithSessionStore(...)`.

Config-backed and inline-built node agents receive the workflow session store
automatically. Prebuilt agents must already use that exact same store instance, otherwise
the workflow fails early. This prevents one workflow run from scattering threads across
multiple stores.

The store must also use the same application event codec as the workflow. Do not manually
pair a store created with a different or core-only codec.

## Choosing A Policy

Use `SharedWorkflowThread` for a single transcript.

Use `ThreadPerAgent` for durable agent-local workspaces.

Use `ForkThreadPerAgent` when several agents should start from the same request and produce separate inspectable threads.

Use no conversation policy for short-lived orchestration where workflow outputs are enough.
