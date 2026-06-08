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
create_conversation_thread
destroy_conversation_thread
get_thread_id
get_message_count
get_thread_messages
add_thread_message
clear_thread
run_agent
run_agent_streaming
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

## JSON Contracts

Agent creation receives JSON for `AgentConfig` plus native function metadata. Thread messages use `ChatMessage` JSON. Streaming events use the same HPD agent event serializer as .NET event streaming.

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
