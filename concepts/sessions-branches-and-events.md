# Sessions, Branches, And Events

HPD Agent uses sessions, branches, and events as the durable coordination model for agent work. They are not only chat-history storage. The same model is used by normal turns, hosted runtimes, subagents, multi-agent workflows, content projection, compaction, replay, and UI navigation.

The short model is:

```text
session: the workspace container for metadata and shared middleware state
branch: a replayable transcript path with branch state, tree metadata, and event history
event: the live and durable vocabulary for rendering, replay, tools, middleware, and nested work
```

In direct in-process code, the built `Agent` owns the runtime and methods such as `CreateSessionAsync(...)`, `RunAsync(...)`, and `ForkBranchAsync(...)` operate against the configured session store.

In ASP.NET Core hosted applications, a runtime scope is selected by:

```text
agentId + sessionId + branchId
```

The `agentId` selects the hosted agent definition or runtime build path. The `sessionId` and `branchId` select the state and branch path used by that runtime agent.

## Sessions

A session is the top-level durable scope for an interaction. It has:

- an id
- creation and last-activity timestamps
- metadata
- session-scoped middleware state

Creating a session also creates the default branch named `main`. Session metadata updates are merge patches: new keys are added, existing keys are overwritten, and null values remove keys.

Use sessions for identity, grouping, UI lists, and state that should be shared across branches. For example, permission choices that apply across a user's branch tree are session-scoped middleware state.

A session can also represent a product workspace. Bots may map a platform thread to one session. A multi-agent workflow can stamp session metadata such as `workspaceKind`, `workflowName`, `workflowExecutionId`, and `conversationMode` so a UI can list the whole workflow run without loading every branch. A normal single-agent chat might use simpler metadata such as customer id, project id, or platform key.

## Branches

A branch is the replayable path inside a session. A branch has:

- an id
- optional name, description, tags, and metadata
- fork metadata such as parent branch, fork point, ancestors, sibling position, and total forks
- branch-scoped middleware state
- a durable branch event log

Branches are the unit for forking, replay, branch-specific state, and hosted runtime ownership. The default `main` branch is protected from deletion.

Forking creates a new branch from a message id on an existing branch. Branch-scoped persistent middleware state is copied at fork time and then diverges between branches.

Branches are also how HPD keeps nested agent work understandable. A subagent can use the parent branch, create a fresh branch, or fork from the parent branch. The default subagent policy forks from the parent session into a hidden child branch, so the child gets the parent's current context without interleaving its transcript with the parent. The child branch metadata points back to the parent session, parent branch, tool call, and subagent run.

Multi-agent workflows use the same branch model, but the application chooses the policy. A workflow can write every node into one shared branch, give each node its own branch, or fork one branch per node from a root workflow branch. Branch metadata marks the workflow name, execution id, node id, agent id, and conversation mode, which gives UI and replay code something stable to group around.

Media-heavy features make this branch scope visible. For example, audio transcript projection, content uploads, and assistant audio artifacts use the active `sessionId + branchId` so replay and forks can keep durable text, media references, and artifacts attached to the correct path. See [Content Upload And Resolution](../guides/content/content-upload-and-resolution.md) for the upload and resolver flow.

## How Features Use The Model

| Feature | Session Use | Branch Use |
|---------|-------------|------------|
| Normal chat | User, case, project, or platform thread container | Active transcript and branch-scoped middleware state |
| Forking | Keeps alternatives grouped together | Creates a new path from a message id |
| Subagents | Parent, new, or shared session depending on policy | Parent, existing, fresh, or forked child branch |
| Multi-agent workflows | Workflow/workspace container with run metadata | Shared, per-agent, or forked node transcripts |
| Compaction | Shared policy and session-scoped state can remain stable | Model-visible history, durable history, or fork target can be reduced |
| Content and audio | Groups durable references by workspace/session | Attaches uploads, artifacts, transcript projection, and replay to one path |
| Hosted runtimes | `agentId + sessionId` selects the runtime scope | `branchId` selects the active path and branch run |

## Branch History Projection

Branches load as projections from durable branch events. The branch event log is the ordered record; the projected branch is the current messages, branch state, and metadata rebuilt from that record.

Branch-history compaction is one projection event family. Default soft compaction preserves durable branch history while reducing what the next model sees. Hard branch-history compaction can remove durable messages from the projected branch and can insert replacement messages such as summaries.

Fork compaction is especially important for nested work. A subagent or multi-agent node can fork from a rich parent/root branch while starting from a smaller model-visible history. That lets a child or node inherit the useful context without copying every durable message into its prompt.

## Events

Events are the framework vocabulary for input, streaming output, lifecycle, tool calls, interactive middleware, retry, background work, and branch-run tracking.

There are two related event surfaces:

- live agent events, used by local subscriptions, hosted SSE, WebSocket, TUI runtimes, and bot adapters
- durable branch events, used to reconstruct branch history and branch projections

Live event envelopes can include routing and correlation fields such as `sessionId`, `branchId`, `channel`, `direction`, trace ids, and metadata when those values are present. Durable branch event JSON is intentionally different and omits many live-routing fields.

Not every live event is written to branch history. Persistence is controlled by each event type's persistence policy and branch-event conversion, not by the event's channel.

This distinction matters for nested agents and workflows. A parent live stream can show subagent or workflow activity as it happens, while the durable transcript may live on a child branch or workflow node branch. Use event metadata for live hierarchy, and use session/branch metadata for durable lookup.

## Runtime Scope Across Hosts

The same model applies across runtime surfaces:

- local agents use the same event vocabulary through typed subscriptions
- hosted APIs expose sessions, branches, branch runs, inputs, SSE, WebSocket, and the bidirectional response route
- TUI runtimes bind UI state to a runtime scope
- bot adapters map platform conversation identity to a session and branch before invoking an agent
- subagents use policy to decide whether child work stays on the parent branch, forks, or moves to another session
- multi-agent workflows use conversation policy to decide where node-agent transcripts are written

This means UI and integration code should treat events as the common rendering and coordination language, while treating hosting routes and local subscriptions as transport choices.

## Concurrency Model

Branch runs and branch operation locks are separate controls.

A hosted branch run is the active model/tool execution for a branch. Only one active hosted run is allowed for a given `sessionId + branchId`, regardless of route `agentId`. A second simultaneous input submission to the same branch returns a conflict, while submissions to different branches can run at the same time.

A branch operation lock protects exclusive branch mutations. Current hosting code uses the explicit branch operation lock for branch deletion. Some other branch operations use broader session-level coordination. Treat operation-lock conflicts as branch mutation conflicts, not as active agent runs.

Middleware responses are also separate from branch runs. A permission, continuation, clarification, or client-tool response is accepted only when the target branch runtime is active and a waiter can accept that response.

Shared branch policies need additional coordination. If two agents write to the same durable branch at once, their branch snapshots and branch middleware state can race. HPD serializes same-branch multi-agent node runs at the conversation route. Separate branches can still run in parallel.

## Related Pages

- [Branch History And Forking](../guides/sessions-and-streaming/branch-history-and-forking.md)
- [Compaction](../guides/sessions-and-streaming/compaction.md)
- [Subagents](../guides/agents/subagents.md)
- [Multi-Agent Conversation Policies](../guides/multi-agent/conversation-policies.md)
- [Hosted Streaming API](../guides/hosting/hosted-streaming-api.md)
- [Hosted Endpoints](../reference/hosted-endpoints.md)
- [Events Reference](../reference/events.md)
- [Audio Overview](../guides/audio/overview.md)
- [Live Evaluation](../guides/evaluations/live-evaluation.md)
