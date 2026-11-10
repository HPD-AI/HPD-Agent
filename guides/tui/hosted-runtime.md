# Hosted TUI Runtime

`HostedAgentTuiRuntime` connects the TUI shell to an ASP.NET Core HPD Agent API. The terminal process becomes a client; the hosted app owns agent definitions, sessions, threads, active runs, and the bidirectional response route.

## Map The Hosted API

```csharp
using HPD.Agent;
using HPD.Agent.AspNetCore;
using HPD.Agent.Providers.OpenAI;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHPD();
builder.Services.AddHPDAgentHost("tui-agent", options =>
{
    options.DefaultAgentId = "tui-agent";
    options.DefaultAgent = new AgentConfig
    {
        Name = "Hosted TUI Agent",
        SystemInstructions = "You are concise and helpful."
    };

    options.ConfigureEachAgentBuilder = agent =>
        agent.WithOpenAI(model: "gpt-5-mini");
});

var app = builder.Build();
app.MapGroup("/hpd").MapHPDAgentHostApi("tui-agent");
await app.RunAsync();
```

The hosted runtime does not call an in-process `Agent`. It sends HTTP requests to the mapped HPD Agent API.

Keep provider composition in the hosted app. The terminal client connects to the mapped API and
does not create the model client itself. If the terminal sends typed provider options, give its
`HostedAgentTuiRuntime` the matching generated `ProviderComposition` so those
options survive the HTTP boundary.

## Connect The TUI

```csharp
using HPD.Agent;
using HPD.Agent.TUI;
using HPD.Agent.TUI.Runtime;

if (!HPDApplicationCompositionHost.TryGetApplication(out var application))
{
    throw new InvalidOperationException(
        "A generated HPD application composition is required.");
}

var scope = new AgentTuiRuntimeScope("tui-agent", "local-session", "main");
await using var runtime = new HostedAgentTuiRuntime(new HostedAgentTuiRuntimeOptions
{
    BaseAddress = new Uri("http://127.0.0.1:5057/hpd/"),
    DefaultScope = scope,
    ProviderComposition = application.Providers,
    EventComposition = application.Events
});

await using var tui = HpdAgentTuiApp.Create(
    runtime,
    new DirectAgentTuiExecutionTarget(scope),
    builder => builder
        .AddAgentTuiDefaults()
        .AddSubAgentTui());

await tui.RunAsync();
```

The TUI executable needs the same generated provider and output-event compositions as the host.
The source-generated application composition registers both. A custom event or provider used by
the server must also be present in the client composition, or the client cannot serialize inputs
or decode the server's event stream.

## Route-Base Warning

`HostedAgentTuiRuntime` uses relative paths such as `sessions`, `agents`, and `agents/{agentId}/sessions/{sessionId}/threads/{threadId}/inputs`.

Set `HostedAgentTuiRuntimeOptions.BaseAddress` to the HPD Agent API route root, not necessarily the web host root.

If your server maps the API at root:

```csharp
app.MapHPDAgentHostApi("tui-agent");
```

use:

```csharp
BaseAddress = new Uri("http://127.0.0.1:5057/")
```

If your server maps the API under a group:

```csharp
app.MapGroup("/hpd").MapHPDAgentHostApi("tui-agent");
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
- cancelling the expected backend-owned execution with structured outcomes
- sending middleware responses for permissions, continuations, clarifications, and client tools

Hosted TUI middleware responses go through hosted response endpoints. Interactive requests are canonical journal events and also appear in runtime control state while they remain answerable, so a reconnecting TUI can restore its prompt controls before transcript catch-up completes. Bot adapters use their platform-specific callback protocols.

`AddSubAgentTui()` registers `/subagents`. It reads the session thread graph and switches to the selected child's `defaultAgentId`, `sessionId`, and `threadId`, allowing the user to continue with the normal child agent after the original delegation finishes.

## Thread Projection And Compaction

The hosted TUI observes the server's session, thread, and compaction behavior. Its thread-fork API
carries the same metadata-oriented fork request supported by the TUI runtime; it does not expose a
compaction toggle. Configure fork-target compaction through the backend agent.

After hard compaction, hosted observation reports the journal rebase. The runtime clears transcript state derived from the old generation, hydrates the replacement generation, and resumes from its new cursor. `CompactionEvent` remains lifecycle information; the checkpoint defines the semantic change.

## Scope Defaults

If `DefaultScope` is not supplied, `HostedAgentTuiRuntime` defaults to:

```text
agentId: default
sessionId: local-session
threadId: main
```

For most hosted apps, pass the scope explicitly so the TUI starts on the intended agent, session, and thread.

`AgentTuiRuntimeScope` requires non-empty agent, session, and thread identifiers. The hosted runtime also rejects malformed endpoint payloads whose required identifier fields are missing or blank; it does not silently construct scopes with empty ids.

## Recovery, Active Executions, And Hydration Errors

The hosted runtime opens a thread by reading `/state`, restoring `activeExecution` and `pendingRequests`, and capturing `observedCursor`. It then opens `/events?after={generation}:{sequence}`, applies catch-up events in bounded batches, and stays on that stream for future commits. If the stream ends after minutes or hours, it reconnects after the last event the TUI fully applied. Backend work continues independently of the observer connection.

`observedCursor` describes the coherent server boundary captured with control state. The TUI advances its applied cursor only after the corresponding event handlers complete successfully. Historical, catch-up, and live batches use the same handlers; batching changes redraw cadence, not projection semantics. A hard journal rebase clears the obsolete transcript projection and hydrates the new generation from sequence zero.

Submission returns the authoritative `threadExecutionId`. The hosted backend remains authoritative if another client starts work.

While idle, Enter submits a `UserMessagesInputEvent`. While an execution is active, Enter retains
a TUI-local follow-up and submits nothing yet. The preview above the editor shows these follow-ups;
`Alt+Up` removes the newest one from the queue and restores it to the composer for editing or
deletion. Escape promotes the oldest follow-up by submitting a `UserMessagesInputEvent` with
`Delivery = AgentInputDelivery.Steer` and the observed `threadExecutionId`. If the execution
finishes first, the TUI submits it normally as the next input. Without a pending follow-up, double
Escape calls `IHpdAgentTuiRuntime.CancelExecutionAsync(scope, threadExecutionId, ...)`, which uses
the hosted execution cancellation route. Pending follow-ups are isolated per runtime scope,
remain local until submitted, and are removed only after the hosted runtime accepts them. A
rejected or failed ordinary submission is restored to the composer, and a stale TUI never
retargets a newer execution automatically.

If an event schema cannot be decoded or projected, hydration stops at the last successfully applied thread position. Register every application-owned event codec in the host and client projection before opening journals that contain that event type.

See [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md) for the underlying protocol.
