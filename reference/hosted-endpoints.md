# Hosted Endpoints

This reference lists the ASP.NET Core hosted runtime endpoints at contract level. Routes are relative to the route group prefix configured by `MapHPDAgentApi(...)`. Created-resource `Location` headers are currently prefixless even when routes are mapped under a prefix.

Endpoint examples use DTO names and status families. Event payload fields are delegated to [Events](events.md), because exhaustive event schemas should be generated from source rather than hand-authored.

## Scope Rules

The hosted runtime scope is:

```text
agentId + sessionId + threadId
```

Routes are intentionally mixed:

- session routes do not include `agentId`
- thread read, update, delete, and content routes are session/thread scoped
- thread create, fork, thread executions, runtime state, semantic input, SSE, and the bidirectional response route include `agentId`
- stored agent definitions live under `/agents`

## Sessions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/sessions` | `CreateSessionRequest?` | `201 SessionDto` | `400` validation problem |
| `GET` | `/sessions` | none | `200 List<SessionDto>` | `400` |
| `POST` | `/sessions/search` | `SearchSessionsRequest?` | `200 List<SessionDto>` | `400` |
| `GET` | `/sessions/{sessionId}` | none | `200 SessionDto` | `404`, `400` |
| `PATCH` | `/sessions/{sessionId}` | `UpdateSessionRequest` | `200 SessionDto` | `404`, `400` |
| `DELETE` | `/sessions/{sessionId}` | none | `204` | `404`, `400` |

`CreateSessionRequest` has optional `agentId`, `sessionId`, and metadata. The registered hosted agent is the owner fallback when `agentId` is omitted; missing session ids are generated. Creating a session creates an agent-owned thread named `main`.

`SearchSessionsRequest` filters by exact stringified metadata value and supports offset and limit. `UpdateSessionRequest` merges metadata and removes keys whose values are null.

## Threads

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/sessions/{sid}/threads` | none | `200 List<ThreadDto>` | `404`, `400` |
| `GET` | `/sessions/{sid}/thread-graph` | none | `200 ThreadGraphDto` | `404`, `400` |
| `GET` | `/sessions/{sid}/threads/{bid}` | none | `200 ThreadDto` | `404`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sid}/threads` | `CreateThreadRequest` | `201 ThreadDto` | `404`, `409`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/fork` | `ForkThreadRequest` | `201 ThreadDto` | `404`, `400` |
| `PATCH` | `/sessions/{sid}/threads/{bid}` | `UpdateThreadRequest` | `200 ThreadDto` | `404`, `400` |
| `DELETE` | `/sessions/{sid}/threads/{bid}?recursive=false` | none | `204` | `404`, `409`, `400` |
| `GET` | `/sessions/{sid}/threads/{bid}/siblings` | none | `200 List<ThreadDto>` | `404`, `400` |

`ForkThreadRequest` has `newThreadId`, `fromMessageId`, optional display metadata, tags, metadata, and an optional polymorphic `compaction` override. The override may inherit configured fork compaction, disable it, or apply an explicit `CompactionSpecification`.

`ForkThreadRequest` requires `fromMessageId`. `main` is protected from deletion. Recursive deletion requires both `recursive=true` and server configuration that allows recursive thread delete.

Every `ThreadDto` includes its required `defaultAgentId`. This is the agent selected when a caller resumes without deliberately choosing another agent; it is not an access restriction. `ThreadGraphDto` returns full thread descriptors, fork groups, and runtime children. A runtime child includes its own `sessionId`, `threadId`, and `defaultAgentId`. `invocationId` identifies the delegation that created a child thread and is not a routing identity.

## Thread Executions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/executions` | none | `200 List<ThreadExecutionDto>` | `404`, `400` |
| `GET` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/executions/{threadExecutionId}` | none | `200 ThreadExecutionDto` | `404`, `400` |

Projected execution status values are `active`, `succeeded`, `failed`, `cancelled`, and recovery-only `interrupted`.

Only one active hosted thread execution is allowed for a given `sessionId + threadId`.

## Agent Definitions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/agents` | `CreateAgentRequest` | `201 StoredAgentDto` | `400` |
| `GET` | `/agents` | none | `200 List<AgentSummaryDto>` | `400` |
| `GET` | `/agents/{agentId}` | none | `200 StoredAgentDto` | `404`, `400` |
| `PUT` | `/agents/{agentId}` | `UpdateAgentRequest` | `200 StoredAgentDto` | `404`, `400` |
| `DELETE` | `/agents/{agentId}` | none | `204` | `404`, `400` |

Create and update validate `AgentConfig`. Listing returns summaries that omit config. Updating or deleting a stored definition evicts cached runtime agents for that `agentId`.

## Content

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/sessions/{sid}/threads/{bid}/content` | `multipart/form-data`, field `file` | `201 ContentDto` | `404`, `400` |
| `GET` | `/sessions/{sid}/threads/{bid}/content` | none | `200 List<ContentDto>` | `404`, `400` |
| `GET` | `/sessions/{sid}/threads/{bid}/content/{contentId}` | none | binary file | `404`, `400` |
| `DELETE` | `/sessions/{sid}/threads/{bid}/content/{contentId}` | none | `204` | `404`, `400` |

Default hosted content storage is in-memory. Do not assume durability unless the host configures durable content storage.

## Streaming And Responses

| Method | Route | Body or protocol | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/inputs` | `StreamTextRequest` or input event envelope | `202 InputSubmissionDto` | `400`, `404`, `409`, `500` |
| `GET` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/state` | none | `200 ThreadRuntimeStateDto` | `404` |
| `GET` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/events?after={generation}:{sequence}` | SSE | catch-up then live `text/event-stream` | `404` before headers, cursor conflict or journal rebase after headers |
| `POST` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/context-usage` | `ContextUsageRequest` | `200 ThreadContextUsage` | `404`, `400`, `500` |
| `POST` | `/agents/{agentId}/sessions/{sid}/threads/{bid}/responses` | serialized `AgentEvent` envelope implementing `IAgentResponseEvent` | `200 AgentRespondResult` | `404`, `400` |

`ThreadRuntimeStateDto` contains `observedCursor`, the backend-owned `activeExecution`, and durable answerable `pendingRequests` owned by that exact thread execution. It does not contain event history. Query a child thread directly for its pending requests. `InputSubmissionDto` contains a `disposition`, the targeted or assigned `threadExecutionId`, optional `startedAt`, and optional authoritative `activeExecution`.

SSE sends committed events after the requested applied cursor, catches up to the current head, and remains open for future commits. Every canonical frame has `id: {Generation}:{ThreadSequenceNumber}`, `event: agent-event`, and the exact serialized event in `data:`. Hard replacement sends a `thread-journal-rebased` control frame and ends the old-generation stream. The server accepts `Last-Event-ID` when `after` is absent; comment heartbeats keep quiet streams alive. HTTP clients post any serialized `AgentEvent` envelope implementing `IAgentResponseEvent` to the single `/responses` route. Ordinary response races are typed `200` results rather than HTTP conflicts.

`/inputs` is the only semantic-input mutation route. Queued inputs return `queued` with a newly assigned execution id. Active controls such as `STEERING_INPUT` and `INTERRUPTION_REQUEST` carry the observed `threadExecutionId` and return `accepted`, `no_active_execution`, `active_execution_mismatch`, `active_input_not_steerable`, or `execution_finishing` as applicable. See [Hosted Lifecycle And Recovery](../guides/hosting/hosted-lifecycle-and-recovery.md).

`context-usage` estimates the scoped thread's current input-token pressure against the supplied `runConfig`, including model context-window metadata when the client has it. It does not start a thread execution and does not compact history.

## Concurrency

Thread executions and thread operation locks are separate:

- thread execution ownership allows one active execution per `sessionId + threadId`
- thread operation locks protect exclusive thread mutations such as deletion
- middleware responses require an active thread runtime but are not thread executions
