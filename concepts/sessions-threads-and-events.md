# Sessions, Threads, And Events

HPD Agent uses sessions, threads, and events as the durable coordination model for agent work. They are not only chat-history storage. The same model is used by normal turns, hosted runtimes, subagents, multi-agent workflows, content projection, compaction, replay, and UI navigation.

The short model is:

```text
session: the workspace container for metadata and shared middleware state
thread: a replayable transcript path with a default agent, thread state, tree metadata, and event history
event: the canonical vocabulary for rendering, replay, tools, middleware, and nested work
```

In direct in-process code, the built `Agent` owns the runtime and methods such as `CreateSessionAsync(...)`, `RunAsync(...)`, and `ForkThreadAsync(...)` operate against the configured session store.

In ASP.NET Core hosted applications, a runtime scope is selected by:

```text
agentId + sessionId + threadId
```

The `agentId` selects the hosted agent definition or runtime build path. The `sessionId` and `threadId` select the state and thread path used by that runtime agent.

## Sessions

A session is the top-level durable scope for an interaction. It has:

- an id
- creation and last-activity timestamps
- metadata
- session-scoped middleware state

Creating a session also creates the default thread named `main`. Session metadata updates are merge patches: new keys are added, existing keys are overwritten, and null values remove keys.

Use sessions for identity, grouping, UI lists, and state that should be shared across threads. For example, permission choices that apply across a user's thread tree are session-scoped middleware state.

A session can also represent a product workspace. Bots may map a platform thread to one session. A multi-agent workflow can stamp session metadata such as `workspaceKind`, `workflowName`, `workflowExecutionId`, and `conversationMode` so a UI can list the whole workflow run without loading every thread. A normal single-agent chat might use simpler metadata such as customer id, project id, or platform key.

## Threads

A thread is the replayable path inside a session. A thread has:

- an id
- a `defaultAgentId` used when continuation does not select another agent
- optional name, description, tags, and metadata
- fork metadata such as parent thread, fork point, ancestors, sibling position, and total forks
- thread-scoped middleware state
- a durable thread event log

Threads are the unit for forking, replay, thread-specific state, and hosted runtime ownership. The default `main` thread is protected from deletion.

Forking creates a new thread from a message id on an existing thread. Thread-scoped persistent middleware state is copied at fork time and then diverges between threads.

Threads are also how HPD keeps nested agent work understandable. Every subagent tool invocation creates a new agent-owned child thread. A fresh child starts with no parent history, a forked child inherits the parent thread, and an isolated child starts empty in a new session. The default policy forks into a hidden child thread in the parent session, so the child gets the current context without interleaving its transcript with the parent. The child descriptor records the agent selected by default for continuation, plus its parent session/thread/tool call and delegation invocation. Callers may deliberately select a different agent for a later run against the completed child thread through the normal thread APIs; the subagent tool itself does not target an existing thread.

Multi-agent workflows use the same thread model, but the application chooses the policy. A workflow can write every node into one shared thread, give each node its own thread, or fork one thread per node from a root workflow thread. Thread metadata marks the workflow name, execution id, node id, agent id, and conversation mode, which gives UI and replay code something stable to group around.

Media-heavy features make this thread scope visible. For example, audio transcript projection, content uploads, and assistant audio artifacts use the active `sessionId + threadId` so replay and forks can keep durable text, media references, and artifacts attached to the correct path. See [Content Upload And Resolution](../guides/content/content-upload-and-resolution.md) for the upload and resolver flow.

## How Features Use The Model

| Feature | Session Use | Thread Use |
|---------|-------------|------------|
| Normal chat | User, case, project, or platform thread container | Active transcript and thread-scoped middleware state |
| Forking | Keeps alternatives grouped together | Creates a new path from a message id |
| Subagents | Parent session for fresh/fork; new session for isolated | A newly created agent-owned fresh, forked, or isolated child thread |
| Multi-agent workflows | Workflow/workspace container with run metadata | Shared, per-agent, or forked node transcripts |
| Compaction | Shared policy can remain stable | Model-visible history or a fork target can be reduced; hard compaction replaces the journal generation |
| Content and audio | Groups durable references by workspace/session | Attaches uploads, artifacts, transcript projection, and replay to one path |
| Hosted runtimes | `agentId + sessionId` selects the runtime scope | `threadId` selects the active path and thread execution |

## Thread History Projection

Threads load as projections from durable thread events. The thread event log is the ordered record; the projected thread is the current messages, thread state, and metadata rebuilt from that record.

Thread-history compaction is one canonical lifecycle. Soft compaction appends a checkpoint while preserving the source journal. Hard compaction atomically replaces the complete journal with a new generation containing the checkpoint, configured semantic result, and authoritative runtime seeds. Replacement messages may contain a summary, or removal compaction may contribute no replacement.

Fork compaction is especially important for nested work. A subagent or multi-agent node can fork from a rich parent/root thread while starting from a smaller model-visible history. That lets a child or node inherit the useful context without copying every durable message into its prompt.

## Events

Events are the framework vocabulary for input, streaming output, lifecycle, tool calls, interactive middleware, retry, background work, and thread-run tracking.

Thread-scoped projectable events have one canonical surface. The framework commits the event first, assigns its authoritative `ThreadSequenceNumber`, and publishes that exact value to subscribers and replay readers. Canonical scope and publication define durability for built-in and application-defined events.

Process-local events still exist for stateless output, diagnostics, struct-event samples, and runtime integrations that are intentionally outside thread history. A stateless `AgentEvent` has no replay guarantee and keeps `ThreadSequenceNumber == 0`.

Nested agents and workflows choose which thread owns their durable facts. Parent observation can still expose routed child activity, while child journals remain independently replayable according to conversation policy. Use event correlation fields for hierarchy and session/thread descriptors for durable lookup.

## Runtime Scope Across Hosts

The same model applies across runtime surfaces:

- local agents use the same event vocabulary through typed subscriptions
- hosted APIs expose sessions, threads, thread executions, authoritative runtime state, inputs, resumable SSE, interruption, and the bidirectional response route
- TUI runtimes bind UI state to a runtime scope
- bot adapters map platform conversation identity to a session and thread before invoking an agent
- subagents create a new agent-owned child thread using fresh, fork, or isolated context policy
- multi-agent workflows use conversation policy to decide where node-agent transcripts are written

This means UI and integration code should treat events as the common rendering and coordination language, while treating hosting routes and local subscriptions as transport choices.

## Concurrency Model

Thread executions and thread operation locks are separate controls.

A hosted thread execution is one accepted input owning the thread's exclusive execution slot. Only one active execution is allowed for a given `sessionId + threadId`; the route `agentId` selects the agent for that execution and need not equal the thread's `defaultAgentId`. A second simultaneous input submission to the same thread returns a conflict, while submissions to different threads can execute at the same time.

A thread operation lock protects exclusive thread mutations. Current hosting code uses the explicit thread operation lock for thread deletion. Some other thread operations use broader session-level coordination. Treat operation-lock conflicts as thread mutation conflicts, not as active agent runs.

Middleware responses are also separate from thread executions. A permission, continuation, clarification, or client-tool response is accepted only when the target thread runtime is active and a waiter can accept that response.

Shared thread policies need additional coordination. If two agents write to the same durable thread at once, their thread snapshots and thread middleware state can race. HPD serializes same-thread multi-agent node executions at the conversation route. Separate threads can still execute in parallel.

## Related Pages

- [Thread History And Forking](../guides/sessions-and-streaming/thread-history-and-forking.md)
- [Canonical Thread Journal](../guides/sessions-and-streaming/canonical-thread-journal.md)
- [Thread Execution Lifecycle](../guides/sessions-and-streaming/thread-execution-lifecycle.md)
- [Compaction](../guides/sessions-and-streaming/compaction.md)
- [Subagents](../guides/agents/subagents.md)
- [Multi-Agent Conversation Policies](../guides/multi-agent/conversation-policies.md)
- [Hosted Streaming API](../guides/hosting/hosted-streaming-api.md)
- [Hosted Endpoints](../reference/hosted-endpoints.md)
- [Events Reference](../reference/events.md)
- [Audio Overview](../guides/audio/overview.md)
- [Live Evaluation](../guides/evaluations/live-evaluation.md)
