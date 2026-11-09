# Application Composition

An agent application has one immutable event composition. It is the complete
compile-time schema used for:

- live event publication;
- durable thread journals and replay;
- event and input catalogs;
- OpenAPI and generated client contracts;
- Native AOT JSON metadata;
- module ownership and durability classification.

The composition is not a runtime subscription list. It is the exact contract
that determines which event types the application can publish, persist, replay,
and expose.

## Durability Is Explicit

Every event is either `Durable` or `LiveOnly`.

- `Durable` means the event is part of the canonical thread journal, replay
  contract, and compatibility digest. Use durable events for thread state,
  conversation content, tool and operation facts, requests and responses,
  goals, subagent authority, and recovery decisions.
- `LiveOnly` means the event may be observed through the runtime event
  coordinator but cannot enter a thread journal. Use it for diagnostics,
  progress, retries, snapshots, provider-call mechanics, and other information
  that can be re-derived or is only useful while the runtime is live.

Custom `AgentEvent` records are live-only unless annotated with
`[DurableEvent]`:

```csharp
using HPD.Agent;
using HPD.Agent.Serialization;

[DurableEvent]
public sealed record RetrievalCompletedEvent(
    string Query,
    int DocumentCount) : AgentEvent;
```

Compositions fail closed when a live-only event is submitted to a durable
journal operation. They also fail when a session store and the selected
composition own different codecs.

## Host Durability Policy

An application may override generated event defaults while building its agent.
The resulting policy is immutable for that agent and its session store:

```csharp
await using var agent = await new AgentBuilder(agentConfig)
    .WithEventDurability<ContentReadEvent>(AgentEventDurability.Durable)
    .WithEventDurability<MyAuditEvent>(AgentEventDurability.LiveOnly)
    .WithChatClient(chatClient)
    .BuildAsync();
```

The string overload selects an event by stable wire discriminator:

```csharp
.WithEventDurability("CONTENT_READ", AgentEventDurability.Durable)
```

Use `WithEventDurabilityPolicy(...)` when the host builds one reusable policy:

```csharp
var policy = AgentEventDurabilityPolicy.Empty
    .Set<ContentReadEvent>(AgentEventDurability.Durable)
    .Set("STATE_SNAPSHOT", AgentEventDurability.Durable);

await using var agent = await new AgentBuilder(agentConfig)
    .WithEventDurabilityPolicy(policy)
    .WithChatClient(chatClient)
    .BuildAsync();
```

The builder applies the policy after selecting the generated application
composition and before creating a builder-owned session store. Therefore the
store and agent share the effective codec. Unknown event types or
discriminators fail construction rather than being ignored.

Recovery-critical events marked `[DurableEvent(Required = true)]` cannot be
demoted to live-only. Libraries can opt into this protection when journal
admission is required for their subsystem to recover correctly.

Changing durability changes the composition's behavior digest, not its
structural event-catalog digest. A TUI or protocol client can continue decoding
the same event schema while the host chooses where events are persisted.

## Normal Applications Need No Setup

Executables are application compositions by default. A project can also opt in:

```xml
<PropertyGroup>
  <HpdAgentApplication>true</HpdAgentApplication>
</PropertyGroup>
```

The source generator closes the application's event and input modules into one
composition and registers it under the application assembly name.

An automatically selected application needs no builder call:

```csharp
await using var agent = await new AgentBuilder(agentConfig)
    .WithChatClient(chatClient)
    .BuildAsync();
```

If exactly one generated application composition is present, `AgentBuilder`
selects it.

## Libraries Contribute Modules

A library normally generates an event module rather than a complete application:

```xml
<PropertyGroup>
  <HpdAgentApplication>false</HpdAgentApplication>
</PropertyGroup>
```

Libraries still define and generate their concrete `AgentEvent` records. When no
complete application composition exists, `AgentBuilder` composes the generated
modules loaded by the process.

An application composition includes its referenced library modules. A module
that is not part of the selected composition is not a valid runtime or
persistence contract.

## Selection Order

`AgentBuilder` resolves the output event composition in this order:

1. An explicit `WithEventComposition(...)` or `AgentConfig.EventComposition`.
2. The exact identity passed to `WithEventApplicationIdentity(...)`.
3. The registered application whose codec is owned by the configured session
   store.
4. The process default application.
5. The sole registered application composition.
6. The combined generated library modules when no application composition is
   available.

If none is available, construction fails with an actionable error. If several
applications are registered and no rule selects one, construction also fails
rather than guessing.

## Multiple Applications In One Process

Use `WithEventApplicationIdentity(...)` only when more than one complete
generated application composition is loaded in the same process:

```csharp
await using var agent = await new AgentBuilder(agentConfig)
    .WithEventApplicationIdentity(typeof(Contoso.WorkerApp).Assembly.GetName().Name!)
    .WithChatClient(chatClient)
    .BuildAsync();
```

The value is the generated application assembly identity. It is not an agent ID,
tenant ID, model ID, or arbitrary display name. If no composition is registered
under that identity, construction fails.

The session store and selected composition must share the exact event codec.
Changing compositions can make an existing journal incompatible even when both
compositions contain events with the same names.

## Explicit Composition

Use `WithEventComposition(...)` when the host intentionally owns a closed schema,
such as a core-only surface or an application-defined composition:

```csharp
var composition = AgentEventComposition.Create([...]);

await using var agent = await new AgentBuilder(agentConfig)
    .WithEventComposition(composition)
    .WithSessionStore(new InMemorySessionStore(composition.Codec))
    .BuildAsync();
```

Do not create a separate session-store codec for the same agent. The builder
validates that a configured store owns the selected composition's codec.

## Serialization

Serialize output events through the selected composition codec:

```csharp
var codec = agent.Config!.EventComposition!.Codec;
var json = codec.Serialize(new TextDeltaEvent("hello", "message-1"));
var evt = codec.DeserializeEvent(json);
```

Semantic inputs use the application's input composition through
`AgentInputCodec`. Hosted endpoints normally perform this work for you.

Do not introduce a process-global event registry or register event metadata at
runtime. The selected generated composition is the only event serialization
authority.

## FFI

`HPD-Agent.FFI` is itself generated as an application composition. Its native
`create_agent_with_ToolHarnesses` entry point selects that composition
internally. Native callers do not call `WithEventApplicationIdentity(...)`.

## Related Pages

- [Events Overview](overview.md)
- [Serialization And Registration](serialization-and-registration.md)
- [Custom Events](custom-events.md)
- [Canonical Thread Journal](../sessions-and-streaming/canonical-thread-journal.md)
