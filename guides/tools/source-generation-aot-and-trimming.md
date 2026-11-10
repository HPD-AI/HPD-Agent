# Source Generation, AOT, And Trimming

The source generator is the production path for native tool harnesses. It emits direct activation, capability registration, function contracts, configuration codecs, middleware projections, and ownership metadata.

## What The Generator Discovers

The generator selects public concrete harness owners containing capabilities such as:

- `[AIFunction]`
- `[Skill]`
- `[SubAgent]`
- `[MultiAgent]`
- `[McpServer]`
- `[OpenApi]`

`[Collapse]` controls visibility and contributes instructions and
expansion-gated middleware. `[ToolHarnessMiddleware]` contributes eager
accepted-input middleware without requiring a collapse container. Neither
attribute is required for capability discovery.

## Generated Artifacts

Each harness library receives:

- `HPD.Agent.Generated.ToolHarnessRegistry.All`.
- A direct activation delegate for the concrete owner.
- Constructor dependency requirements and owner-local defaults.
- Direct function creation delegates.
- Closed JSON Schemas and strict argument binders.
- Typed argument carriers and runtime parameter injection.
- Metadata codecs.
- Immutable settings schemas and codecs.
- Generated presence-aware run-override types.
- Middleware configuration section projections.
- Separate ordered expansion-gated and eager middleware descriptors.
- MCP and OpenAPI source collectors.
- Collapse metadata and inherited composition.
- A module initializer that registers the generated catalog.

## Constructor Contract

A native harness has exactly one public constructor.

```csharp
public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
}
```

The generator emits `context => new SearchHarness(...)` and resolves each constructor parameter directly. It does not inspect constructors at runtime.

`WithToolHarness<T>()` does not impose a `new()` constraint; generated activation owns construction.

## Dependency Defaults

```csharp
[ToolHarnessDependency(typeof(ISearchClient), typeof(SearchClient))]
public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
}
```

The generated catalog can create `SearchClient` when application DI has no registration. HPD owns defaults it creates; DI and registration-local overrides remain borrowed.

## Configuration Codecs

The harness settings type is an immutable data contract:

```csharp
public sealed record SearchSettings
{
    [HarnessRunSetting]
    public int MaxResults { get; init; } = 10;

    public string Endpoint { get; init; } = "https://search.example";
}
```

The generator emits:

- library-default construction
- schema descriptions
- generated JSON read/write delegates
- constructor binding
- run-override presence tracking
- reset validation
- inherited base views

Application code should not create a parallel `JsonSerializerContext` for these settings. The harness package carries its generated contract.

## Cross-Assembly Harnesses

A harness can live in a referenced assembly or NuGet package:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

`WithToolHarness<T>()` loads that assembly's generated registry and resolves the exact owner type.

An external base class and immutable base settings type communicate their generated contracts through compiled metadata. The consumer does not need source code, but the library must have been built with the current generator.

## Configuration Loading

Construction JSON uses `toolHarnesses`:

```json
{
  "toolHarnesses": [
    {
      "name": "SearchHarness",
      "config": {
        "maxResults": 20
      }
    }
  ]
}
```

Run JSON uses `harnesses`:

```json
{
  "harnesses": [
    {
      "name": "SearchHarness",
      "config": {
        "maxResults": 5
      }
    }
  ]
}
```

The outer HPD serializer parses the envelope. The registered harness library supplies the opaque config codec. Unknown fields, duplicate keys, wrong scalar types, and invalid effective settings fail before tool construction or run execution.

## Runtime Lookup

`WithToolHarness<T>()`:

- runs the target assembly's generated module initializer;
- loads its registry;
- resolves the exact concrete type;
- activates one owner instance;
- retains it for the built agent.

`WithTool<T>("FunctionName")` uses the generated function-name list to validate the selected capability from that owner.

## Native AOT

Generated activation and configuration paths are Native AOT compatible when every included harness library carries its generated metadata.

The AOT acceptance path must cover:

- external-library constructor activation;
- immutable settings decoding;
- a typed or serialized run override;
- middleware configuration access;
- function execution;
- deterministic owner disposal.

The framework smoke tests publish and execute these paths; publishing an unused assembly is insufficient.

External integration packages use the same generated composition boundary:

- MCP AgentConfig activation and `[McpServer]` declarations use generated
  application composition and AOT-safe MCP serializers.
- `[OpenApi]` sources are collected through generated ToolHarness registration.
- `WithMcp` and `WithOpenApi` use the same generated capability catalog as native
  functions.

The MCP qualification job publishes and executes a native binary covering
discovery, tool listing, invocation, permissions, and serialization. The OpenAPI
test suite exercises flat and collapsed builder registration on every supported
.NET target.

The AOT fixture covers both policies:

- `Collapse.Middlewares` activates when its harness expands;
- `[ToolHarnessMiddleware]` activates for the accepted input without requiring
  expansion.

Both use generated construction, ownership, configuration, and Native AOT
activation descriptors. Reflection registration rejects either middleware
declaration.

## Diagnostics

Important source-generation diagnostics:

- `HPDHIN009`: harness has no public constructor.
- `HPDHIN010`: harness has multiple public constructors.
- `HPDCFG001`: unsupported or unverifiable immutable settings shape.
- `HPDCFG002`: invalid middleware settings section projection.
- `HPD020`: unsupported runtime parameter; use `FunctionExecutionContext` when appropriate.
- `HPDAG0101`–`HPDAG0103`: conflicting collapse declarations.
- `HPDAG0201`–`HPDAG0204`: harness/middleware declaration problems.
- `HPDAG0213`: empty eager middleware declaration.
- `HPDAG0214`: same middleware type declared with both activation policies.
- `HPDAG0301`–`HPDAG0304`: MCP source problems.
- `HPDAG0401`–`HPDAG0403`: OpenAPI source problems.

Runtime configuration errors include `HarnessNotRegistered`, `HarnessConfigUnknownProperty`, `HarnessRunOverrideNotSupported`, `HarnessResetConflict`, and `HarnessConfigurationOwnerMismatch`.

## Troubleshooting

`ToolHarness 'WeatherTools' not found in ToolHarnessRegistry.All` means the concrete owner was not generated.

Check that:

- the class is public;
- it contains a recognized capability;
- the project references the source generator;
- exactly one public constructor exists;
- external libraries were rebuilt after adopting the configuration contract.

See [Configure A Tool Harness](configure-a-tool-harness.md) and [Tool Harness Configuration Reference](../../reference/harness-configuration.md).
