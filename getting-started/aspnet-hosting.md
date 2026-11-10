# ASP.NET Hosting

ASP.NET Core hosting turns an agent runtime into HTTP endpoints for sessions, threads, content, streaming, middleware responses, and stored agent definitions.

Use hosting when a web app, TypeScript client, TUI, or another process needs to talk to HPD Agent over a boundary.

## What You Are Creating

`AddHPDAgentHost("getting-started-agent", ...)` creates one **host profile**. The profile owns the
agent store, session store, runtime services, and HTTP surface. It can contain many stored agent
definitions; this first sample declares one default agent named `getting-started-agent`.

The resulting identities form a hierarchy:

```text
ASP.NET application
└── host profile: getting-started-agent
    ├── stored agents and runtime instances
    ├── sessions
    └── threads
```

See [Host Profiles And Agents](../concepts/host-profiles-and-agents.md) for the full model.

## Create A Web App

```bash
dotnet new web -n HpdAgentHost
cd HpdAgentHost
dotnet add package HPD-Agent.AspNetCore --version 0.9.5
dotnet add package HPD-Agent.Providers.OpenAI --version 0.9.5
```

Set an OpenAI API key:

```bash
export OPENAI_API_KEY="..."
```

## Add Program.cs

```csharp
using HPD.Agent;
using HPD.Agent.AspNetCore;
using HPD.Agent.Providers.OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHPDAgentHost("getting-started-agent", options =>
{
    var dataRoot = Path.Combine(Directory.GetCurrentDirectory(), ".hpd-hosting");

    options.SessionStorePath = Path.Combine(dataRoot, "sessions");
    options.AgentStore = new JsonAgentStore(Path.Combine(dataRoot, "agents"));
    options.DefaultAgentId = "getting-started-agent";

    options.DefaultAgent = new AgentConfig
    {
        Name = "Getting Started Agent",
        SystemInstructions = "You are a hosted HPD Agent. Be concise and helpful."
    };

    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini");
});

var app = builder.Build();

app.MapGet("/", () => "HPD Agent is hosted.");

app.MapGroup("/hpd").MapHPDAgentHostApi("getting-started-agent");

app.Run();
```

Run it:

```bash
dotnet run
```

Verify the host is listening:

```bash
curl http://localhost:5000/
```

Expected:

```text
HPD Agent is hosted.
```

Before exposing hosted routes outside local development, decide how your app will handle authentication, authorization, CORS, storage durability, rate limits, and streaming client lifecycle.

## Send The First Request

The root endpoint only proves that ASP.NET started. Complete one hosted-agent flow to verify that
the profile, stored definition, session, thread, and model are wired together.

Create a session using the default agent:

```bash
curl -X POST http://localhost:5000/hpd/sessions \
  -H "Content-Type: application/json" \
  -d '{"agentId":"getting-started-agent"}'
```

Copy the returned session `id`, then list its threads:

```bash
curl http://localhost:5000/hpd/sessions/{sessionId}/threads
```

Copy the thread `id` from the returned `main` thread and submit a message:

```bash
curl -X POST \
  http://localhost:5000/hpd/agents/getting-started-agent/sessions/{sessionId}/threads/{threadId}/inputs \
  -H "Content-Type: application/json" \
  -d '{"text":"Explain what a host profile is in one sentence."}'
```

The request returns `202 Accepted`. Read authoritative control state:

```bash
curl \
  http://localhost:5000/hpd/agents/getting-started-agent/sessions/{sessionId}/threads/{threadId}/state
```

Open the committed event stream for transcript and live output:

```bash
curl -N \
  "http://localhost:5000/hpd/agents/getting-started-agent/sessions/{sessionId}/threads/{threadId}/events?after=1:0"
```

You have now exercised the hosted path end to end. The deeper [Hosted Streaming API](../guides/hosting/hosted-streaming-api.md) explains cursor recovery, steering, interruption, and middleware responses.

## What Happens

`AddHPDAgentHost(...)` registers a named host profile and installs the immutable provider
composition generated from the provider packages referenced by this application. The profile can
host multiple stored agent definitions and declares one default definition at startup.

`SessionStorePath` creates a segmented `FileSessionStore` for hosted sessions and canonical thread journals.

`AgentStore` stores hosted agent definitions. This sample uses `JsonAgentStore`.

`PersistAfterTurn` defaults to `true` on `HPDAgentHostOptions`. The hosted agent therefore saves
session metadata and ensures the active thread descriptor exists after a completed turn. Canonical
thread events are committed through the configured session store independently. This host option
is separate from choosing `SessionStorePath`.

At startup, hosting creates the definition named by `DefaultAgentId` from `DefaultAgent` when that
definition does not already exist. If `DefaultAgentId` is omitted, it defaults to the hosting
registration name. Runtime requests resolve stored definitions only; unknown agent ids return
`404` and are never synthesized.

`MapHPDAgentHostApi(...)` maps the built-in HPD Agent endpoints under `/hpd`.

`ConfigureEachAgentBuilder` runs after each stored definition has loaded and after the host has
attached its stores and services. It applies to every agent built by this profile, may add
capabilities or override individual settings, and is not written back to the definition store.

The hosting layer owns persistence and passes the configured stores to runtimes as requests arrive. Configure stores through hosting options rather than creating separate stores inside `ConfigureEachAgentBuilder`.

When a hosted input request arrives, hosting resolves the route `agentId`, reserves thread-run ownership, commits the start fact, and queues the input into that runtime. The backend owns committed events, middleware responses, client tools, permissions, and interruptions. Hosted clients read control state from `/state` and use one `/events?after={appliedCursor}` SSE stream for catch-up and future commits. For the direct-run versus started-runtime model, see [Agent Runtime And Capabilities](../concepts/agent-runtime-and-capabilities.md).

## Next

At this point, the first path is complete: build an agent, stream it, add a tool, keep session history, save state, and host the runtime.

Choose a next track:

| Track | Start Here |
| --- | --- |
| Build a web client | [Hosted Streaming API](../guides/hosting/hosted-streaming-api.md) and [TypeScript Client Events](../guides/events/typescript-client.md) |
| Host multiple agents | [Host Profiles And Agents](../concepts/host-profiles-and-agents.md) and [Stored Agent Definitions](../guides/hosting/stored-agent-definitions.md) |
| Expose a workflow over HTTP | [ASP.NET Multi-Agent Workflows](../guides/hosting/multiagent-aspnet.md) |
| Add real tool surfaces | [Author A Tool Harness](../guides/tools/author-a-tool-harness.md), [MCP Servers](../guides/tools/mcp-servers.md), and [OpenAPI Tools](../guides/tools/openapi-tools.md) |
| Publish a contract or client | [ASP.NET OpenAPI Contracts](../guides/hosting/openapi-contract.md) |
| Add safety and control | [Middleware Overview](../guides/middleware/overview.md) and [Permissions Middleware](../guides/middleware/permissions.md) |
| Add orchestration | [Subagents](../guides/agents/subagents.md) and [Multi-Agent Overview](../guides/multi-agent/overview.md) |
| Ship the runtime | [ASP.NET Core Hosting](../guides/hosting/aspnet-core.md), [Stored Agent Definitions](../guides/hosting/stored-agent-definitions.md), and [Logging And Telemetry](../guides/observability/logging-and-telemetry.md) |

For a short orchestration tutorial, continue to [Build A Multi-Agent Workflow](agent-workflow.md).
