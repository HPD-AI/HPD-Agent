# Branch History And Forking

Branches let one session hold multiple replayable paths. A branch can be created directly, forked from a message id, updated for UI metadata, listed with sibling/tree information, and deleted subject to branch protection rules.

There are two public ways to work with branches:

- Direct in-process `Agent` APIs, where your code calls methods on a built `Agent` that has a session store.
- ASP.NET Core hosted APIs, where clients use HTTP routes and DTOs exposed by `MapHPDAgentApi(...)`.

## Direct Agent API

Use direct APIs when your application owns the `Agent` instance:

```csharp
var sessionId = await agent.CreateSessionAsync("review-session");

await agent.RunAsync(
    "Draft the first answer.",
    sessionId: sessionId,
    branchId: "main");

var forkId = await agent.ForkBranchAsync(
    sessionId,
    sourceBranchId: "main",
    newBranchId: "short-answer",
    fromMessageId: messageId);
```

Direct APIs require a configured session store. `CreateSessionAsync(...)` creates the default `main` branch. `ForkBranchAsync(...)` returns the new branch id and can also accept `BranchForkOptions` for programmatic fork behavior such as fork compaction.

## ASP.NET Core Hosted Routes

Branch read, update, delete, content, and event-log routes are scoped by `sessionId` and `branchId`. Branch create and fork routes also include `agentId` because they need the hosted runtime configuration for that agent scope.

| Operation | Route |
| --- | --- |
| List branches | `GET /sessions/{sessionId}/branches` |
| Get branch | `GET /sessions/{sessionId}/branches/{branchId}` |
| Create branch | `POST /agents/{agentId}/sessions/{sessionId}/branches` |
| Fork branch | `POST /agents/{agentId}/sessions/{sessionId}/branches/{branchId}/fork` |
| Update branch | `PATCH /sessions/{sessionId}/branches/{branchId}` |
| Delete branch | `DELETE /sessions/{sessionId}/branches/{branchId}?recursive=false` |
| Get branch events | `GET /sessions/{sessionId}/branches/{branchId}/events` |
| Get siblings | `GET /sessions/{sessionId}/branches/{branchId}/siblings` |

## Hosted Create A Branch

Creating a branch accepts a `CreateBranchRequest` with a branch id plus optional name, description, tags, and metadata. If the branch id is blank, the service can generate one.

Creating a branch with an existing id returns a conflict. A branch created this way starts as its own path rather than a fork from another branch.

## Hosted Fork From A Message

Forking accepts a `ForkBranchRequest`:

```json
{
  "newBranchId": "try-short-answer",
  "fromMessageId": "message-id-to-fork-from",
  "name": "Short answer",
  "description": "A shorter response path",
  "tags": ["draft"],
  "metadata": {
    "uiColor": "green"
  }
}
```

`fromMessageId` is required. If the message id is not present on the source branch, the hosted API returns a validation error.

The new branch records its source branch and fork point. Persistent branch-scoped middleware state is copied to the new branch when the fork is committed and then evolves independently.

## Hosted Update Branch Metadata

Branch updates accept optional name, description, tags, and metadata. Metadata is merged: values add or overwrite keys, and null removes keys.

Use branch metadata for UI labels, filters, annotations, and runtime hints that belong to a single path.

## Delete Branches

The `main` branch is protected and cannot be deleted.

In the hosted API, deleting a branch with children returns a conflict unless `recursive=true`. Recursive deletion must also be enabled by server configuration. If recursive deletion is disabled, the API returns a validation error even when the request includes `recursive=true`.

Hosted branch deletion uses a branch operation lock. A lock conflict means another exclusive branch mutation is in progress; it does not mean an agent run is active.

## Read Branch Events

`GET /sessions/{sessionId}/branches/{branchId}/events` returns normalized polymorphic `AgentEvent` JSON for the branch log. This is useful for rebuilding branch views and debugging history.

Do not assume the branch event JSON is identical to live SSE or WebSocket event envelopes. Live event envelopes can include routing and correlation fields when they are present on the event. Durable branch event JSON may omit live-routing fields and only contains events that were mapped or opted into branch persistence.

In direct in-process code, read branch history through the configured session store or higher-level runtime that wraps it. In ASP.NET Core hosted clients, use the branch events route.

For UI projection guidance, see [Render An Event Stream](render-an-event-stream.md). For the live-vs-durable model, see [Event Streams And Hierarchies](../../concepts/event-streams-and-hierarchies.md).

## Branch History Compaction

Hard branch-history compaction changes the projected branch. A `BRANCH_HISTORY_COMPACTED` durable branch event removes compacted durable message ids from projection and inserts replacement messages when the retention mode produced them.

Soft compaction does not change the durable branch projection. It only reduces what the next model turn sees.

For the full model, see [Compaction](compaction.md).

## Forking After Compaction

Forking uses a message id on the projected source branch. If hard compaction removed an older message from the projection, that original message id may no longer be a valid fork point. The framework can surface replacement candidates when a compacted-away id is detected, but clients should not assume automatic recovery.

Use projected branch messages as the source of forkable message ids.

## Fork And Compact

Fork compaction is a separate pre-commit behavior. When configured, the target branch is compacted after it is copied from the source branch and before the target branch is saved.

The source branch is unchanged. The target branch starts with the already-compacted initial history. Fork compaction does not write a standalone `BRANCH_HISTORY_COMPACTED` event.

Hosted `ForkBranchRequest` does not expose a per-request compaction intent today. Hosted fork compaction is controlled by server-side agent and middleware configuration.

The middleware hook for this phase is `BeforeBranchForkCommitAsync`. It receives the source branch, the in-memory target branch, the fork point, and `BranchForkOptions`. This is the place for middleware that needs to change the target branch before it becomes durable. After the fork is committed, durable branch events such as `BRANCH_FORKED` describe the committed branch state; they are not mutation hooks.

## Sibling Navigation

The hosted sibling endpoint returns branch DTOs with tree and sibling fields such as sibling index, total siblings, previous sibling id, next sibling id, original branch id, and total forks.

Current implementation returns full `BranchDto` values for siblings. Avoid depending on a separate lightweight sibling DTO until that contract is reconciled.
