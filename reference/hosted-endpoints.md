# Hosted Endpoints

This reference lists the ASP.NET Core hosted runtime endpoints at contract level. Routes are relative to the route group prefix configured by `MapHPDAgentHostApi(...)`. Created-resource `Location` headers include the mapped route prefix.

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

## Mapping Surfaces

`MapHPDAgentHostApi(...)` maps the complete surface and remains the full reference-host
composition. Applications can instead map separate groups:

| Mapper | Surface |
| --- | --- |
| `MapHPDAgentRuntimeApi(...)` | sessions, threads, content, streaming, runtime state, executions, responses, compatibility catalogs, and storage capabilities |
| `MapHPDAgentClientToolProvidersApi()` | client-tool provider inspection and development attachment |
| `MapHPDAgentAdministrationApi(...)` | stored agent definitions and runtime-extension schema management |

Each mapper returns a `RouteGroupBuilder`, so runtime and administration routes can receive
different ASP.NET Core authorization policies:

```csharp
var hpd = app.MapGroup("/hpd");

hpd.MapHPDAgentRuntimeApi("assistant")
   .RequireAuthorization("AgentUser");

hpd.MapGroup("/admin")
   .MapHPDAgentAdministrationApi("assistant")
   .RequireAuthorization("AgentAdmin");
```

The client-tool connection included by `MapHPDAgentClientToolProvidersApi()` and the full
composite is not authorized by an `IClientToolProviderConnectionAuthorizer`. Production gateways
should map `MapAuthorizedHPDClientToolProviderConnection(...)` instead.

## Storage Capabilities

`GET /storage` describes the session, stored-agent, and content stores without requiring a
particular implementation:

```json
{
  "sessions": {
    "persistence": "Ephemeral",
    "configured": false,
    "usingDefaultFallback": true
  },
  "agentDefinitions": {
    "persistence": "RestartDurable",
    "configured": true,
    "usingDefaultFallback": false
  },
  "content": {
    "persistence": "Ephemeral",
    "configured": false,
    "usingDefaultFallback": true
  }
}
```

In-memory stores are valid defaults, not an unhealthy state. Custom store implementations report
their own persistence capability through the store interface.

## Sessions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/sessions` | `CreateSessionRequest?` | `201 SessionDto` | `400` validation problem |
| `GET` | `/sessions` | none | `200 List<SessionDto>` | `400` |
| `POST` | `/sessions/search` | `SearchSessionsRequest?` | `200 List<SessionDto>` | `400` |
| `GET` | `/sessions/{sessionId}` | none | `200 SessionDto` | `404`, `400` |
| `PATCH` | `/sessions/{sessionId}` | `UpdateSessionRequest` | `200 SessionDto` | `404`, `400` |
| `DELETE` | `/sessions/{sessionId}` | none | `204` | `404`, `400` |

`CreateSessionRequest` has optional `agentId`, `sessionId`, and metadata. When `agentId` is
omitted, hosting uses the profile's declared `DefaultAgentId`. A missing or unknown explicit agent
definition returns `404`. Missing session ids are generated. Creating a session through the hosted
API creates an agent-owned thread named `main`.

The initial thread is a convenience default. It is not protected and can be deleted. A session
can remain valid with zero threads after its last thread is deleted.

`SearchSessionsRequest` filters by exact stringified metadata value and supports offset and limit. `UpdateSessionRequest` merges metadata and removes keys whose values are null.

## Threads

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/sessions/{sessionId}/threads` | none | `200 List<ThreadDto>` | `404`, `400` |
| `GET` | `/sessions/{sessionId}/thread-graph` | none | `200 ThreadGraphDto` | `404`, `400` |
| `GET` | `/sessions/{sessionId}/threads/{threadId}` | none | `200 ThreadDto` | `404`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads` | `CreateThreadRequest` | `201 ThreadDto` | `404`, `409`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/fork` | `ForkThreadRequest` | `201 ThreadForkResultDto` | `404`, `400` |
| `PATCH` | `/sessions/{sessionId}/threads/{threadId}` | `UpdateThreadRequest` | `200 ThreadDto` | `404`, `400` |
| `DELETE` | `/sessions/{sessionId}/threads/{threadId}?recursive=false` | none | `204` | `404`, `409`, `400` |

`ForkThreadRequest` has `newThreadId`, `fromMessageId`, optional display metadata, tags, metadata, and an optional polymorphic `compaction` override. The override may inherit configured fork compaction, disable it, or apply an explicit `CompactionSpecification`.

`fromMessageId` is optional. A string forks through that committed message; `null` creates a root
fork before any messages. `main` is deletable like any other thread. Deleting a thread with
children returns a validation error unless `recursive=true`, and recursive deletion also requires
server configuration that allows it.

Every `ThreadDto` includes its required `defaultAgentId`. This is the agent selected when a caller resumes without deliberately choosing another agent; it is not an access restriction. `ThreadGraphDto` returns full thread descriptors, fork groups, and runtime children. A runtime child includes its own `sessionId`, `threadId`, and `defaultAgentId`. `invocationId` identifies the delegation that created a child thread and is not a routing identity.

## Thread Executions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/executions` | none | `200 List<ThreadExecutionDto>` | `404`, `400` |
| `GET` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/executions/{threadExecutionId}` | none | `200 ThreadExecutionDto` | `404`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/executions/{threadExecutionId}/cancel` | none | `200` cancellation result | `404`, `409`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/queue/start` | none | `200` queue-promotion result | `404`, `409`, `400` |

Projected execution status values are `active`, `succeeded`, `failed`, `cancelled`, and recovery-only `interrupted`.

Only one active hosted thread execution is allowed for a given `sessionId + threadId`.

The dedicated `cancel` route targets one exact runtime-owned execution and waits for its terminal
fact. Steering uses `/inputs` with a `UserMessagesInputEvent` whose `delivery` is `Steer` and whose
`threadExecutionId` matches the active execution observed from `/state`. `queue/start` resumes
queue promotion after an accepted hosted cancellation.

## Agent Definitions

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/agents` | `CreateAgentRequest` | `201 StoredAgentDto` | `400`, `409` |
| `GET` | `/agents` | none | `200 List<AgentSummaryDto>` | `400` |
| `GET` | `/agents/{agentId}` | none | `200 StoredAgentDto` | `404`, `400` |
| `PUT` | `/agents/{agentId}` | `UpdateAgentRequest` | `200 StoredAgentDto` | `404`, `400` |
| `DELETE` | `/agents/{agentId}` | none | `204` | `404`, `400` |

`CreateAgentRequest` requires `agentId`, `name`, and `config`. Creating a duplicate id returns
`409 Conflict`. Create and update validate `AgentConfig`. Listing returns summaries that omit
config. Updating or deleting a stored definition evicts cached runtime agents for that `agentId`.

Each `AddHPDAgentHost(...)` host profile contains an agent store and may contain many definitions.
At startup it declares one default definition using `DefaultAgentId`, which defaults to the
registration name. The startup declaration is created only when the id is absent. Runtime requests
resolve stored definitions only and never synthesize, persist, or recreate one.

## Content

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/sessions/{sessionId}/threads/{threadId}/content` | `multipart/form-data`, field `file` | `201 ContentDto` | `404`, `400` |
| `GET` | `/sessions/{sessionId}/threads/{threadId}/content` | none | `200 List<ContentDto>` | `404`, `400` |
| `GET` | `/sessions/{sessionId}/threads/{threadId}/content/{contentId}` | none | binary file | `404`, `400` |
| `DELETE` | `/sessions/{sessionId}/threads/{threadId}/content/{contentId}` | none | `204` | `404`, `400` |

Default hosted content storage is in-memory. Do not assume durability unless the host configures durable content storage.

## Streaming And Responses

| Method | Route | Body or protocol | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/inputs` | `StreamTextRequest` or input event envelope | `202 InputSubmissionDto` | `400`, `404`, `409`, `500` |
| `GET` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/state` | none | `200 ThreadRuntimeStateDto` | `404` |
| `GET` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/events?after={generation}:{sequence}` | SSE | catch-up then live `text/event-stream` | `404` before headers, cursor conflict or journal rebase after headers |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/context-usage` | `ContextUsageRequest` | `200 ThreadContextUsage` | `404`, `400`, `500` |
| `POST` | `/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses` | serialized `AgentEvent` envelope implementing `IAgentResponseEvent` | `200 AgentRespondResult` | `404`, `409`, `400` |

`ThreadRuntimeStateDto` contains `observedCursor`, the backend-owned `activeExecution`, and durable answerable `pendingRequests` owned by that exact thread execution. It does not contain event history. Query a child thread directly for its pending requests. `InputSubmissionDto` contains a `disposition`, the targeted or assigned `threadExecutionId`, optional `startedAt`, and optional authoritative `activeExecution`.

SSE sends committed events after the requested applied cursor, catches up to the current head, and remains open for future commits. Every canonical frame has `id: {Generation}:{ThreadSequenceNumber}`, `event: agent-event`, and the exact serialized event in `data:`. Hard replacement sends a `thread-journal-rebased` control frame and ends the old-generation stream. The server accepts `Last-Event-ID` when `after` is absent; comment heartbeats keep quiet streams alive. HTTP clients post any serialized `AgentEvent` envelope implementing `IAgentResponseEvent` to the single `/responses` route. Ordinary response races are typed `200` results rather than HTTP conflicts.

`/inputs` is the only semantic-input mutation route. An ordinary `UserMessagesInputEvent` with
`delivery: "Queue"` returns `queued` with a newly assigned execution id. A steering
`UserMessagesInputEvent` carries `delivery: "Steer"` and the observed `threadExecutionId`; it
returns `accepted`, `no_active_execution`, `active_execution_mismatch`,
`active_input_not_steerable`, or `execution_finishing` as applicable. Cancellation uses the
dedicated execution `cancel` route. See [Hosted Lifecycle And Recovery](../guides/hosting/hosted-lifecycle-and-recovery.md).

`context-usage` estimates the scoped thread's current input-token pressure against the supplied `runConfig`, including model context-window metadata when the client has it. It does not start a thread execution and does not compact history.

## Multi-Agent Workflows

The multi-agent activation API is mapped separately with `MapHPDMultiAgentApi(...)`. It currently
exposes workflow discovery and durable run enqueue, not the same session/thread surface as a hosted
chat agent:

| Method | Route | Body or protocol | Success |
| --- | --- | --- | --- |
| `GET` | `/multi-agent/workflows` | none | `200 MultiAgentWorkflowListResponse` |
| `POST` | `/multi-agent/workflows/{workflowId}/runs` | `MultiAgentRunRequest` and `Idempotency-Key` header | durable activation acceptance |

See [ASP.NET Multi-Agent Workflows](../guides/hosting/multiagent-aspnet.md) for setup requirements
and the current run-observation boundary.

## Concurrency

Thread executions and thread operation locks are separate:

- thread execution ownership allows one active execution per `sessionId + threadId`
- thread operation locks protect exclusive thread mutations such as deletion
- middleware responses require an active thread runtime but are not thread executions
