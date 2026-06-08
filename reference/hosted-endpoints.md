# Hosted Endpoints

This reference lists the ASP.NET Core hosted runtime endpoints at contract level. Routes are relative to the route group prefix configured by `MapHPDAgentApi(...)`. Created-resource `Location` headers are currently prefixless even when routes are mapped under a prefix.

Endpoint examples use DTO names and status families. Event payload fields are delegated to [Events](events.md), because exhaustive event schemas should be generated from source rather than hand-authored.

## Scope Rules

The hosted runtime scope is:

```text
agentId + sessionId + branchId
```

Routes are intentionally mixed:

- session routes do not include `agentId`
- branch read, update, delete, content, and event-log routes are session/branch scoped
- branch create, fork, branch runs, input, interrupt, SSE, WebSocket, and the bidirectional response route include `agentId`
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

`CreateSessionRequest` has optional `sessionId` and metadata. Missing ids are generated. Creating a session creates branch `main`.

`SearchSessionsRequest` filters by exact stringified metadata value and supports offset and limit. `UpdateSessionRequest` merges metadata and removes keys whose values are null.

## Branches

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/sessions/{sid}/branches` | none | `200 List<BranchDto>` | `404`, `400` |
| `GET` | `/sessions/{sid}/branches/{bid}` | none | `200 BranchDto` | `404`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sid}/branches` | `CreateBranchRequest` | `201 BranchDto` | `404`, `409`, `400` |
| `POST` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/fork` | `ForkBranchRequest` | `201 BranchDto` | `404`, `400` |
| `PATCH` | `/sessions/{sid}/branches/{bid}` | `UpdateBranchRequest` | `200 BranchDto` | `404`, `400` |
| `DELETE` | `/sessions/{sid}/branches/{bid}?recursive=false` | none | `204` | `404`, `409`, `400` |
| `GET` | `/sessions/{sid}/branches/{bid}/events` | none | `200 List<AgentEvent>` | `404`, `400` |
| `GET` | `/sessions/{sid}/branches/{bid}/siblings` | none | `200 List<BranchDto>` | `404`, `400` |

`ForkBranchRequest` has `newBranchId`, `fromMessageId`, optional display metadata, tags, and metadata. It does not have a per-request fork-compaction field. Hosted fork compaction is controlled by the configured server-side agent and middleware pipeline.

`ForkBranchRequest` requires `fromMessageId`. `main` is protected from deletion. Recursive deletion requires both `recursive=true` and server configuration that allows recursive branch delete.

## Branch Runs

| Method | Route | Body | Success | Common failures |
| --- | --- | --- | --- | --- |
| `GET` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/runs` | none | `200 List<BranchRunDto>` | `404`, `400` |
| `GET` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/runs/active` | none | `200 BranchRunDto?` | `404`, `400` |
| `GET` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/runs/{runtimeRunId}` | none | `200 BranchRunDto` | `404`, `400` |

Branch run status values are currently `active`, `completed`, `cancelled`, and `failed`.

Only one active hosted branch run is allowed for a given `sessionId + branchId`.

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
| `POST` | `/sessions/{sid}/branches/{bid}/content` | `multipart/form-data`, field `file` | `201 ContentDto` | `404`, `400` |
| `GET` | `/sessions/{sid}/branches/{bid}/content` | none | `200 List<ContentDto>` | `404`, `400` |
| `GET` | `/sessions/{sid}/branches/{bid}/content/{contentId}` | none | binary file | `404`, `400` |
| `DELETE` | `/sessions/{sid}/branches/{bid}/content/{contentId}` | none | `204` | `404`, `400` |

Default hosted content storage is in-memory. Do not assume durability unless the host configures durable content storage.

## Streaming And Responses

| Method | Route | Body or protocol | Success | Common failures |
| --- | --- | --- | --- | --- |
| `POST` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/inputs` | `StreamTextRequest` or input event envelope | `202` | `400`, `404`, `409`, `500` |
| `GET` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/events/live` | SSE | `text/event-stream` | `404` before headers |
| `POST` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/interrupt` | optional interruption envelope or reason body | `202` | `404`, `409`, `400`, `500` |
| `GET` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/ws` | WebSocket text frames | WebSocket stream | pre-upgrade `400`, `404`, `409`; post-upgrade invalid-payload or policy-violation close statuses |
| `POST` | `/agents/{agentId}/sessions/{sid}/branches/{bid}/responses` | bidirectional response event envelope | `200` | `404`, `409`, `400` |

SSE sends one live event envelope per `data:` frame. WebSocket accepts text frames containing input envelopes or bidirectional response envelopes and sends live event envelopes back after a valid client frame initializes the subscription. HTTP clients post permission, continuation, clarification, client-tool, or custom bidirectional response envelopes to the single `/responses` route.

## Concurrency

Branch runs and branch operation locks are separate:

- branch run ownership allows one active hosted run per `sessionId + branchId`
- branch operation locks protect exclusive branch mutations such as deletion
- middleware responses require an active branch runtime but are not branch runs
