# Stored Agent Definitions

Stored agent definitions are the agent identities exposed by a hosted application. A runtime
route `agentId` must resolve to one of these definitions; an unknown id returns `404`.

The stored definition is not the running conversation. It tells hosting what agent to build for a route `agentId`. Session and thread routes then select the durable conversation path, and streaming/input routes start or reuse the active runtime for that `agentId + sessionId + threadId` scope.

## Host Profiles And Definitions

A **host profile** is the runtime container. It owns the agent store, session store, content store,
runtime managers, and API surface. A profile can contain many stored agent definitions:

```text
host profile: assistant
├── agent definition: researcher
├── agent definition: writer
└── agent definition: reviewer
```

Each profile seeds one startup definition:

- the `AddHPDAgentHost(...)` registration name selects the host profile
- `DefaultAgentId` names the profile's startup definition
- `DefaultAgentId` defaults to the profile registration name
- additional definitions are created through `POST /agents`

Use [Host Profiles And Agents](../../concepts/host-profiles-and-agents.md) when deciding whether a
new requirement needs another agent definition or another isolated profile.

## Definition Routes

| Operation | Route | Body | Success |
| --- | --- | --- | --- |
| Create definition | `POST /agents` | `CreateAgentRequest` | `201 StoredAgentDto` |
| List definitions | `GET /agents` | none | `200 List<AgentSummaryDto>` |
| Get definition | `GET /agents/{agentId}` | none | `200 StoredAgentDto` |
| Update definition | `PUT /agents/{agentId}` | `UpdateAgentRequest` | `200 StoredAgentDto` |
| Delete definition | `DELETE /agents/{agentId}` | none | `204` |

Create and update validate the supplied `AgentConfig`. `POST /agents` requires an explicit
`agentId`; a duplicate id returns `409 Conflict`.

## Summary Versus Full Definition

`AgentSummaryDto` is intended for list views and omits the full config. `StoredAgentDto` includes the stored config and metadata for detail/editing views.

Use the list route for catalogs and the detail route when you need to inspect or edit the definition.

## Create And Update Bodies

Create accepts:

```json
{
  "agentId": "support-assistant",
  "name": "support-assistant",
  "config": {
    "clients": {
      "chat": {
        "provider": {
          "key": "openai"
        },
        "modelName": "gpt-5-mini"
      }
    }
  },
  "metadata": {
    "team": "support"
  }
}
```

Update accepts a replacement config body:

```json
{
  "config": {
    "clients": {
      "chat": {
        "provider": {
          "key": "openai"
        },
        "modelName": "gpt-5-mini"
      }
    }
  }
}
```

Keep provider setup aligned with the provider docs. Chat model configuration lives under `clients.chat`.

## Shared Runtime Customization

`ConfigureEachAgentBuilder` is a host-profile callback, not part of a stored definition. It runs
after the selected definition and host services have configured each new agent builder:

```csharp
options.ConfigureEachAgentBuilder = agent =>
    agent.WithOpenAI(model: "gpt-5-mini");
```

This applies to every host-created agent, but its changes are not written back into `/agents`.
Operations such as `WithInstructions(...)` or `WithOpenAI(...)` can override matching stored
settings, while other builder operations may add capabilities. If agents need different chat
models, branch on the loaded agent configuration inside the callback or define provider selection
per stored agent.

The callback is skipped when a custom `IAgentFactory` is registered.

## Cache Eviction

Updating or deleting a stored definition evicts cached unscoped and agent/thread-scoped runtime
instances for that `agentId`. The next request rebuilds from the current definition. A deleted
definition is not recreated by runtime resolution.

This is important for hosted UIs: a successful update does not mutate an already active thread execution in place, but it does affect subsequent runtime builds.

## Factory Override

If an `IAgentFactory` is registered in dependency injection, it owns construction and is the first
build source. The manager passes the requested route `agentId`. The factory path returns the built
agent directly, so it bypasses host builder enrichment and
`HPDAgentHostOptions.ConfigureEachAgentBuilder`.

Use a factory when agent construction needs application services or dynamic policy that should not
be stored as an `AgentConfig`. The factory must attach the event composition, session store, and
other runtime infrastructure required by the hosted path.
