# ASP.NET Core Hosting

HPD Agent hosting exposes sessions, threads, stored agent definitions, authoritative runtime state, input submission, resumable committed events, content, and middleware response endpoints through ASP.NET Core minimal APIs.

The core hosting model is:

```text
AddHPDAgentHost(...) registers a hosting profile
MapHPDAgentRuntimeApi(...) maps the application runtime surface
MapHPDAgentClientToolProvidersApi(...) maps client-tool attachment routes
MapHPDAgentAdministrationApi(...) maps stored definitions and administration
agentId + sessionId + threadId selects a runtime scope inside the profile
```

A host profile owns infrastructure and may contain many stored agent definitions. At startup it
declares one default definition. `DefaultAgentId` names that definition and defaults to the profile
name. Additional definitions can be created through the administration API.

Use [Host Profiles And Agents](../../concepts/host-profiles-and-agents.md) for the lifecycle
difference between the profile, a stored definition, a built agent, a session, a thread, and a
runtime instance.

## Minimal Setup

```csharp
using HPD.Agent;
using HPD.Agent.AspNetCore;
using HPD.Agent.Providers.OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHPDAgentHost("assistant", options =>
{
    options.DefaultAgentId = "assistant";
    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini")
             .WithInstructions("You are a concise helpful assistant.");
});

var app = builder.Build();

app.MapGroup("/hpd").MapHPDAgentRuntimeApi("assistant");

app.Run();
```

Before the server accepts requests, hosting creates `/agents/assistant` if that definition does
not already exist. This maps the application-facing session, thread, content, streaming,
runtime-state, execution, middleware-response, event-catalog, and input-catalog routes. Runtime
requests resolve only definitions that exist in the agent store.

## Route Prefix

Compose the prefix with normal ASP.NET routing:

```csharp
var hpd = app.MapGroup("/api/hpd");
hpd.MapHPDAgentRuntimeApi("assistant");
```

With this prefix, a sessions request becomes `POST /api/hpd/sessions`.

`MapHPDAgentHostApi(...)` remains available as the complete composite mapping. It includes the
runtime, client-tool, and stored-definition surfaces and honors `HPDAgentEndpointOptions` flags
for reference hosts.

## Mapping Surfaces And Authorization

Map each surface onto a separate route group so it can receive independent authorization:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AgentUser", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("AgentAdmin", policy => policy.RequireRole("AgentAdmin"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

var hpd = app.MapGroup("/hpd");

hpd.MapHPDAgentRuntimeApi("assistant")
   .RequireAuthorization("AgentUser");

hpd.MapGroup("/client-tools")
   .MapHPDAgentClientToolProvidersApi()
   .RequireAuthorization("AgentUser");

hpd.MapGroup("/admin")
   .MapHPDAgentAdministrationApi("assistant")
   .RequireAuthorization("AgentAdmin");
```

`RequireAuthorization(...)` is standard ASP.NET Core endpoint metadata. You must also register
authentication and authorization services, define the named policies, and add the corresponding
middleware. The HPD library does not choose an authentication scheme.

`MapHPDAgentClientToolProvidersApi()` retains the development attachment route, which does not
require an `IClientToolProviderConnectionAuthorizer`. Production application-gateway deployments
should map `MapAuthorizedHPDClientToolProviderConnection(...)` instead.

## Named Host Profiles

Use named profiles when one ASP.NET Core app needs independent storage, policies, or API
boundaries:

```csharp
builder.Services.AddHPDAgentHost("support", options =>
{
    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini")
             .WithInstructions("You help with support requests.");
});

builder.Services.AddHPDAgentHost("research", options =>
{
    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini")
             .WithInstructions("You help with research tasks.");
});

app.MapGroup("/support-agent").MapHPDAgentRuntimeApi("support");
app.MapGroup("/research-agent").MapHPDAgentRuntimeApi("research");
```

The hosting name selects the server-side profile. The URL prefix is whatever you configure. Each
profile declares its default agent using `DefaultAgentId`, falling back to the registration name.
Additional agent definitions are created explicitly through that profile's `POST /agents` route.

## Host Profile Contents

Each named profile owns its own:

- session manager
- agent manager
- agent store
- session store
- content store
- hosting service facade

If `SessionStorePath` is configured, hosting creates a segmented `FileSessionStore`. Otherwise, the default session store is `InMemorySessionStore`. The default agent store is also in-memory.

`GET /storage` reports whether each store was explicitly configured or selected as the default
fallback, plus whether the implementation declares `Ephemeral` or `RestartDurable` persistence.
In-memory is a valid implementation, not an error; deployment policy decides whether an ephemeral
store is acceptable.

`PersistAfterTurn` defaults to `true` for `HPDAgentHostOptions`. After a completed turn, the hosted
agent saves session metadata and ensures the active thread descriptor exists. Canonical thread
events are committed through the configured session store independently. Set the option to `false`
only when the application explicitly owns that post-turn bookkeeping.

Created-resource `Location` headers include the mapped route prefix. Content uploads use a hosting
content store scoped to the thread. The default hosting service creates an in-memory content store,
so do not treat uploaded content as durable unless a durable content store is configured.

## Agent Build Resolution

At host startup, each registered hosting profile ensures that its declared agent definition
exists. `DefaultAgent`, `DefaultAgentDocument`, and `DefaultAgentPath` are definition sources for
that startup declaration, not per-request fallback sources. Existing definitions are not
overwritten.

When a hosted runtime needs an agent, the manager resolves it in this order:

1. `IAgentFactory` from dependency injection, if registered.
2. The stored definition for the route `agentId`.

An unknown `agentId` returns `404`. Runtime requests never synthesize, persist, or resurrect a
definition. `IAgentFactory` remains an advanced escape hatch for applications that own dynamic
runtime construction.

When the profile creates an agent from a stored definition, hosting enriches the builder with the
service provider, route agent id, agent store, session store, content store, event composition,
and configured hosting options. `ConfigureEachAgentBuilder` then runs last for that agent instance.

The callback may add runtime capabilities or override individual settings. Its changes are applied
to the built agent but are not written back to the stored definition. It runs when a runtime
instance is built, not on every request, because built runtimes are cached by agent/session/thread
scope.

The callback is skipped when a custom `IAgentFactory` owns construction. In that case the factory
must attach the required event composition, session store, service provider, and middleware or
server policy itself.

Configure stores through hosting options. Avoid configuring a separate session store inside
`ConfigureEachAgentBuilder`, because hosted runtime state should use the host profile's store.

## Runtime Scope

Runtime routes include:

```text
/agents/{agentId}/sessions/{sessionId}/threads/{threadId}/...
```

That scope is used by HTTP input, state, interrupt, resumable SSE, thread executions, and the bidirectional response route. TUI hosted runtimes and bot adapters should map their UI or platform identity into the same runtime scope before sending input or responses.

Hosted clients should load `/state` to restore `activeExecution` and `pendingRequests`, then open `/events?after={appliedCursor}`. A client without local projection starts at zero and rebuilds the complete conversation incrementally. See [Hosted Lifecycle And Recovery](hosted-lifecycle-and-recovery.md).

## Related Pages

- [Host Profiles And Agents](../../concepts/host-profiles-and-agents.md)
- [Stored Agent Definitions](stored-agent-definitions.md)
- [Hosted Streaming API](hosted-streaming-api.md)
- [Hosted Lifecycle And Recovery](hosted-lifecycle-and-recovery.md)
- [ASP.NET Multi-Agent Workflows](multiagent-aspnet.md)
- [Hosted Endpoints Reference](../../reference/hosted-endpoints.md)
