# Host Profiles And Agents

Host profiles are the deployment and lifecycle boundary for hosted HPD agents. An agent definition
is one identity and configuration inside that boundary.

This distinction matters because one ASP.NET application can host several profiles, and one profile
can host many agents.

## Hierarchy

```text
ASP.NET application
└── host profile: assistant
    ├── stores and runtime managers
    ├── agent definition: researcher
    ├── agent definition: writer
    ├── session: workspace-42
    │   ├── thread: main
    │   └── thread: alternative-review
    └── runtime instances built for agent/session/thread scopes
```

## Host Profile

A host profile is created by `AddHPDAgentHost(...)`:

```csharp
builder.Services.AddHPDAgentHost("assistant", options =>
{
    options.SessionStorePath = "App_Data/assistant/sessions";
    options.AgentStore = new JsonAgentStore("App_Data/assistant/agents");
    options.DefaultAgentId = "assistant";

    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini");
});
```

The profile owns:

- the agent store containing stored definitions
- the session store containing sessions and thread journals
- the content store
- the agent and session managers
- input and event compositions
- runtime caching and idle eviction
- provider and runtime policy shared by its agents
- the HTTP authorization and route-prefix boundary configured when mapping it

Profiles created with different names have separate manager and store instances unless the
application deliberately assigns the same store objects.

Configuring a session store and `PersistAfterTurn` are separate concerns. The hosted option
defaults to `true`; after each completed turn, the agent saves session metadata and ensures the
active thread descriptor exists. Canonical thread events are committed through the configured
store independently.

## Agent Definition

An agent definition is a stored record containing an `agentId`, display name, `AgentConfig`, and
optional metadata:

```json
{
  "agentId": "researcher",
  "name": "Researcher",
  "config": {
    "name": "Researcher",
    "systemInstructions": "Research evidence and cite sources."
  }
}
```

The definition is not a conversation and is not a running agent. Hosting loads the definition and
builds an agent from it when a runtime is needed.

A profile declares one definition at startup through `DefaultAgent` or one of its file/configuration
counterparts. It can contain additional definitions created through `POST /agents`.

## Agent And Runtime Instance

The built `Agent` is the executable runtime object. Hosted runtimes are cached by a scope:

```text
agentId + sessionId + threadId
```

Creating a new session or thread can therefore produce a new runtime instance from the same stored
agent definition. Runtime state such as run queues and live event bridges belongs to that instance,
not to the reusable definition.

`ConfigureEachAgentBuilder` runs when a host-built agent instance is created. It applies after the
stored definition and host stores have configured the builder. The callback can add runtime
capabilities or override individual settings, but its changes are not written back to the stored
definition.

The callback is skipped when a custom `IAgentFactory` owns construction.

## Session And Thread

A session is a persistence-capable workspace that can contain several threads. It is restart-durable
when the profile is configured with a durable session store. A thread is one conversation path
inside that session. The thread may carry a default agent id for continuation, while runtime routes
still include the agent id selected for the execution.

Use the same profile for agents that share infrastructure but have different roles:

```text
/api/agents/researcher/...
/api/agents/writer/...
/api/agents/reviewer/...
```

Use separate profiles when the agents need different storage, security policy, route boundaries, or
operational ownership:

```csharp
builder.Services.AddHPDAgentHost("internal", internalOptions);
builder.Services.AddHPDAgentHost("customer", customerOptions);

app.MapGroup("/api/internal").MapHPDAgentRuntimeApi("internal");
app.MapGroup("/api/customer").MapHPDAgentRuntimeApi("customer");
```

There is no fixed small limit on the number of profiles. Treat each profile as an isolation and
resource-management boundary rather than a synonym for one agent.

## Related Pages

- [ASP.NET Core Hosting](../guides/hosting/aspnet-core.md)
- [Stored Agent Definitions](../guides/hosting/stored-agent-definitions.md)
- [Sessions, Threads, And Events](sessions-threads-and-events.md)
- [Agent Builder And Agent](agent-builder-and-agent.md)
