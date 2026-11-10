# FFI Overview

The FFI project publishes HPD Agent as a Native AOT shared library for native hosts.

Use this surface when a non-.NET host needs to create agents, manage conversation threads, run agents, receive streaming events, or respond to permission requests through a C ABI.

## What The Library Exports

The macOS arm64 publish output produces:

```text
HPD-Agent.FFI.dylib
```

The exported symbols include:

```text
ping
free_string
create_agent_with_ToolHarnesses
destroy_agent
list_agent_operations
cancel_agent_operation
begin_provider_authorization
complete_provider_authorization
advance_provider_device_authorization
get_provider_device_authorization_status
cancel_provider_device_authorization
get_provider_authorization_status
disconnect_provider_account
revoke_provider_account
revoke_and_disconnect_provider_account
create_conversation_thread
destroy_conversation_thread
get_thread_id
get_message_count
get_thread_messages
add_thread_message
clear_thread
run_agent
run_agent_streaming
hpd_agent_subscribe_events
hpd_subscription_dispose
respond_to_permission
```

Native code receives opaque handles for managed objects. It should not treat those handles as direct managed object pointers.

## Publish Shape

The FFI project is configured as a Native AOT shared library.

A validated macOS arm64 publish shape is:

```bash
dotnet restore src/HPD-Agent.FFI/HPD-Agent.FFI.csproj -r osx-arm64 -p:TargetFramework=net10.0
dotnet restore src/HPD-Agent.SourceGenerator/HPD-Agent.SourceGenerator.csproj
dotnet publish src/HPD-Agent.FFI/HPD-Agent.FFI.csproj \
  -f net10.0 \
  -c Release \
  -r osx-arm64 \
  --no-restore \
  -p:PublishAot=true
```

The output also includes native dependencies such as ONNX Runtime libraries when they are part of the publish graph.

## String Ownership

Strings returned by HPD FFI exports are allocated by the FFI library. Native callers must release them with:

```text
free_string
```

Streaming callbacks receive event pointers that are valid only during the callback. Do not store those pointers after the callback returns.

## Thread-Routed Event Subscriptions

`hpd_agent_subscribe_events` creates a persistent subscription rooted at one complete
`(session_id, thread_id)` key. The hierarchy argument is one of the frozen
`hpd_agent_event_hierarchy` values. The callback receives a UTF-8 JSON
`AgentEventDelivery` envelope with `event` and `route`; the route identifies the origin
thread, root-to-origin path, and optional thread execution id.

The callback JSON buffer is borrowed and valid only for the callback. The returned
`hpd_subscription*` is caller-owned and must be released with `hpd_subscription_dispose`.
Disposing from inside that same callback is rejected with
`HPD_SUBSCRIPTION_DISPOSE_FROM_CALLBACK`; schedule disposal after the callback returns.

The checked-in native contract header is
`dotnet/HPD-Agent.Framework/src/HPD-Agent.FFI/hpd_agent_events.h`. It defines the hierarchy
enum, callback signature, subscription status values, and disposal status values.

## JSON Contracts

Agent creation receives JSON for `AgentConfig` plus native function metadata.
Thread messages use `ChatMessage` JSON. Streaming events use the generated
application composition selected inside `HPD-Agent.FFI`.

The FFI project is generated as an application composition. Its
`create_agent_with_ToolHarnesses` entry point selects that composition
internally through its assembly identity. Native callers do not select a
composition and do not pass an assembly name.

Event JSON is part of the integration contract:

- output events are encoded by the composition's `AgentEventCodec`;
- discriminators, durability, module ownership, and JSON metadata are fixed at
  FFI build time;
- unknown or incompatible events fail closed instead of being skipped;
- the FFI build and the .NET host must be upgraded together when event contracts
  change.

See [Application Composition](../events/application-composition.md) for the
selection model and [Serialization And Registration](../events/serialization-and-registration.md)
for output and input codec responsibilities.

Serialized `AgentRunConfig` documents may include a `harnesses` array of per-run setting overrides. The override is bound only after the FFI host has registered the matching harness library and its generated configuration codec. Executable dependency instances and runtime configuration handles are never portable.

See [Configure A Harness Per Run](../tools/configure-a-harness-per-run.md) and [Tool Harness Configuration Reference](../../reference/harness-configuration.md).

Native tool harness integration expects a separate native library named:

```text
hpd_native_ToolHarnesses
```

That library must provide the native tool registry and execution symbols expected by the FFI layer.

## Verification Surface

The FFI test suite covers:

- Native AOT shared-library publish
- exported symbol presence for core entry points
- a small C host loading the published library and calling `ping`/`free_string`
- native tool-harness registry, schema, stats, function-list, executor registration, and function execution
- managed agent execution through FFI test helpers, including streaming callback delivery and permission response routing

ABI stability and version negotiation are separate contract concerns from symbol availability and smoke execution.
