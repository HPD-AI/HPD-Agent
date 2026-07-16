# Hosted TUI Runtime

`HostedAgentTuiRuntime` connects the TUI shell to an ASP.NET Core HPD Agent API. The terminal process becomes a client; the hosted app owns agent definitions, sessions, threads, active runs, and the bidirectional response route.

## Map The Hosted API

```csharp
using HPD.Agent;
using HPD.Agent.AspNetCore;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting();
builder.Services.AddHPDAgent("tui-agent", config =>
{
    config.DefaultAgent = new AgentConfig
    {
        Name = "Hosted TUI Agent",
        SystemInstructions = "You are concise and helpful.",
        Clients = new AgentClientConfig
        {
            Chat = new ClientProviderConfig
            {
                ProviderKey = "openai",
                ModelName = "gpt-5-mini"
            }
        }
    };
});

var app = builder.Build();
app.MapGroup("/hpd").MapHPDAgentApi("tui-agent");
await app.RunAsync();
```

The hosted runtime does not call an in-process `Agent`. It sends HTTP requests to the mapped HPD Agent API.

The host project must reference and register the provider package used by `ProviderKey = "openai"` following the provider docs. Keep provider registration in the hosted app; the terminal client connects to the mapped API and does not create the model client itself.

## Connect The TUI

```csharp
using HPD.Agent.TUI;
using HPD.Agent.TUI.Runtime;

var scope = new AgentTuiRuntimeScope("tui-agent", "local-session", "main");
await using var runtime = new HostedAgentTuiRuntime(new HostedAgentTuiRuntimeOptions
{
    BaseAddress = new Uri("http://127.0.0.1:5057/hpd/"),
    DefaultScope = scope
});

await using var tui = HpdAgentTuiApp.Create(
    runtime,
    scope,
    builder => builder.AddAgentTuiDefaults());

await tui.RunAsync();
```

## Route-Base Warning

`HostedAgentTuiRuntime` uses relative paths such as `sessions`, `agents`, and `agents/{agentId}/sessions/{sessionId}/threads/{threadId}/inputs`.

Set `HostedAgentTuiRuntimeOptions.BaseAddress` to the HPD Agent API route root, not necessarily the web host root.

If your server maps the API at root:

```csharp
app.MapHPDAgentApi("tui-agent");
```

use:

```csharp
BaseAddress = new Uri("http://127.0.0.1:5057/")
```

If your server maps the API under a group:

```csharp
app.MapGroup("/hpd").MapHPDAgentApi("tui-agent");
```

use:

```csharp
BaseAddress = new Uri("http://127.0.0.1:5057/hpd/")
```

Do not point the hosted TUI at `http://127.0.0.1:5057/` when the HPD API is actually under `/hpd`.

## Runtime Calls

The hosted runtime uses the hosted API for:

- listing, loading, creating, updating, and deleting stored agent definitions
- listing, searching, loading, creating, renaming, and deleting sessions
- listing, creating, forking, renaming, and deleting threads
- loading authoritative runtime control state with the journal boundary, active run, and pending interactions
- catching up and observing committed thread events through one resumable SSE stream
- submitting `AgentInputEvent` instances
- interrupting the expected backend-owned run with structured outcomes
- sending middleware responses for permissions, continuations, clarifications, and client tools

Hosted TUI middleware responses go through hosted response endpoints. Interactive requests are canonical journal events and also appear in runtime control state while they remain answerable, so a reconnecting TUI can restore its prompt controls before transcript catch-up completes. Bot adapters use their platform-specific callback protocols.

## Thread Projection And Compaction

The hosted TUI observes the server's session, thread, and compaction behavior. It can fork threads through the hosted API. Fork compaction follows the server-side agent and middleware configuration.

After hard durable thread-history compaction, the projected thread history is canonical. Render the thread as loaded from the hosted API or thread event projection, and treat compaction events as audit/debug metadata.

## Scope Defaults

If `DefaultScope` is not supplied, `HostedAgentTuiRuntime` defaults to:

```text
agentId: default
sessionId: local-session
threadId: main
```

For most hosted apps, pass the scope explicitly so the TUI starts on the intended agent, session, and thread.

`AgentTuiRuntimeScope` requires non-empty agent, session, and thread identifiers. The hosted runtime also rejects malformed endpoint payloads whose required identifier fields are missing or blank; it does not silently construct scopes with empty ids.

## Recovery, Active Runs, And Hydration Errors

The hosted runtime opens a thread by reading `/state`, restoring `activeRun` and `pendingRequests`, and capturing `observedHead`. It then opens `/events?after={appliedCursor}`, applies catch-up events in bounded batches, and stays on that stream for future commits. If the stream ends after minutes or hours, it reconnects after the last event the TUI fully applied. Backend work continues independently of the observer connection.

`observedHead` describes the coherent server boundary captured with control state. The TUI advances its applied cursor only after the corresponding event handlers complete successfully. Historical, catch-up, and live batches use the same handlers; batching changes redraw cadence, not projection semantics.

Submission returns the authoritative `runtimeRunId`. `HpdAgentTuiApp` prevents a second prompt submission while that run is active, while the hosted backend remains authoritative if another client starts work.

Escape cancellation reads the active run and sends it as `expectedRuntimeRunId`. The backend can report `accepted`, `already_terminal`, `no_active_run`, or `active_run_mismatch`; a stale TUI therefore cannot accidentally cancel a newer run. Reopening the same scope uses the same state-and-cursor path to restore output and cancellation controls.

If an event schema cannot be decoded or projected, hydration stops at the last successfully applied thread position. Register every application-owned event codec in the host and client projection before opening journals that contain that event type.

See [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md) for the underlying protocol.
