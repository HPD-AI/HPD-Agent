# Serialization And Registration

Event serialization is governed by the active generated application composition.
There is no process-global event serializer and no runtime event-registration
step.

See [Application Composition](application-composition.md) for how an executable,
library, or multi-application host selects its composition.

## Output Event Envelope

Output events use a flat envelope:

```json
{
  "version": "1.0",
  "type": "TEXT_DELTA",
  "text": "hello",
  "messageId": "message-id"
}
```

Version `1.0` is the only accepted envelope version. Decoding rejects a missing,
null, or unsupported `version` instead of silently reading a future or
malformed contract with the current schema.

The concrete event's source-generated JSON metadata supplies the payload. The
composition supplies:

- the stable `type` discriminator;
- durability classification;
- owning module identity;
- compatibility digest;
- allowed JSON contract.

The catalog exposes a structural contract digest for each event. The digest
covers the serialized property shape rather than the CLR type name.

Durable events form the replay contract. Live-only events remain observable but
cannot be appended to a thread journal. Diagnostics, progress, retries, raw
snapshots, and provider-storage mechanics are live-only; canonical state and
recovery facts are durable.

An application can override overridable defaults through the
`AgentBuilder.WithEventDurability(...)` methods before the session store is
created. Required recovery events reject demotion. The effective policy changes
the composition behavior digest while preserving its structural event catalog.

## Serialize Output Events

Local subscriptions receive typed events. Serialize only where a value crosses a
process, hosted API, log, tool, or client boundary:

```csharp
var codec = agent.Config!.EventComposition!.Codec;

using var all = agent.SubscribeAny(evt =>
{
    var json = codec.Serialize(evt);
    stream.Write(json);
});
```

The same codec should be used for live and historical delivery. If another store
or transport reads the journal, it must use that store's exact codec.

## Deserialize Output Events

```csharp
var codec = agent.Config!.EventComposition!.Codec;
AgentEvent evt = codec.DeserializeEvent(json);

if (evt is TextDeltaEvent delta)
    Console.Write(delta.Text);
```

Unknown discriminators fail with `UnknownAgentEventDiscriminatorException`.
They are not silently ignored or converted into an untyped event.

## Input Events Are Separate

Semantic inputs such as `UserMessagesInputEvent` use the application's input
composition, not the output-event codec. For a custom transport, create an input
codec bound to the same provider and input compositions used by the host:

```csharp
var inputCodec = new AgentInputCodec(providerComposition, inputComposition);
var json = inputCodec.Serialize(new UserMessagesInputEvent
{
    Messages = [new ChatMessage(ChatRole.User, "hello")]
});

AgentInputEvent input = inputCodec.DeserializePublic(json);
```

Use the public deserializer only for events that the input composition marks as
publicly submittable. Untrusted clients must not be able to construct internal
inputs merely because the CLR type is reachable.

Hosted ASP.NET endpoints normally perform input decoding, public-input
validation, provider selection, and run admission for you.

## Custom Events

Define a concrete `AgentEvent` record:

```csharp
public sealed record RetrievalProgressEvent(
    string Query,
    int DocumentsScanned,
    int DocumentsMatched) : AgentEvent;
```

The source generator adds it to the application composition or library event
module. Use `[EventType(...)]` to choose a stable wire discriminator:

```csharp
[EventType("RETRIEVAL_PROGRESS_V2")]
public sealed record RetrievalProgressEvent(
    string Query,
    int DocumentsScanned,
    int DocumentsMatched) : AgentEvent;
```

No output-event registration call is required or supported. The generated
composition owns discriminator and JSON metadata selection.

Computed C# convenience properties are omitted from serialization unless they
are explicitly declared as contract properties. Serialize the underlying
canonical facts instead.

For Native AOT, provide the source-generated JSON metadata expected by the
event contract and let the application composition close the module. The
composition carries that metadata into the resulting codec.

## Registration Rules

- Executables generate and register one complete application composition.
- Libraries generate event modules and must not create a competing application
  composition unless they are intended to run as the application.
- Referenced modules must be part of the selected application composition.
- Duplicate discriminators, duplicate module identities, unknown JSON metadata,
  live-only durable publication, and incompatible store codecs fail closed.
- `WithEventApplicationIdentity(...)` selects an existing generated application;
  it does not install or register one.

## Related Pages

- [Application Composition](application-composition.md)
- [Custom Events](custom-events.md)
- [Events Reference](../../reference/events.md)
- [Canonical Thread Journal](../sessions-and-streaming/canonical-thread-journal.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
