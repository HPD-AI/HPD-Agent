# Source Generation, AOT, And Trimming

HPD Agent's tool-harness path is designed around generated metadata. The generator discovers harnesses and emits factories so agent build can register functions without reflecting over methods in the normal path.

## What Gets Generated

The generator selects classes with `[Collapse]` and classes with methods carrying capability attributes such as `[AIFunction]`, `[Skill]`, `[SubAgent]`, `[MCPServer]`, or `[OpenApi]`.

Generated artifacts include:

- Per-harness registration classes.
- `HPD.Agent.Generated.ToolHarnessRegistry.All`.
- Direct constructor delegates.
- Direct `CreateFunctions` delegates.
- Function-name arrays for selective registration.
- Metadata deserializers for generic capability attributes such as `[AIFunction<TMetadata>]`.
- Config constructor delegates for supported `*Config` constructor parameters.
- `ISecretResolver` constructor delegates.
- External source collectors for harness-owned MCP and OpenAPI sources.
- Collapse middleware factories for supported middleware constructors.
- A module initializer that registers generated tool harnesses.

Publicly accessible, instantiable harnesses are the reliable target for generated registry inclusion. Classes without `[Collapse]` are still discovered when they contain capability methods; they simply remain non-collapsed.

Instantiable means the generator can create a factory for the harness shape. Supported generated construction paths include:

- Public parameterless constructors.
- Public single config constructors.
- Public `ISecretResolver` constructors.

## Cross-Assembly Harnesses

A tool harness can live in a referenced library. The source generator runs in the project that defines the harness, and that assembly carries its own generated `ToolHarnessRegistry`.

For example, an application can reference a coding harness package and register a harness from that package:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<CodingHarness>()
    .BuildAsync();
```

`WithToolHarness<T>()` runs the module initializer for `typeof(T).Assembly`, loads that assembly's generated registry, and resolves the harness from the registry entry emitted by the harness library. The harness does not need to be defined in the application project.

Stored agent configuration can also reference generated harnesses by name once the relevant harness assembly has been loaded and its generated catalog has registered.

## JSON Metadata Contract

Generated factories avoid reflection-based JSON hydration. When a generated factory needs to hydrate JSON, it resolves a `JsonTypeInfo<T>` and uses the source-generation-friendly serializer overload.

This applies to:

- Tool harness config constructor types.
- Typed tool metadata from generic capability attributes such as `[AIFunction<TMetadata>]`.
- Collapse-scoped middleware config types.
- Standalone middleware config types.

Any type hydrated from JSON must have source-generated JSON metadata available to HPD at runtime.

For a harness library, place the metadata beside the harness:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(CodingHarnessConfig))]
[JsonSerializable(typeof(CodingMiddlewareConfig))]
public partial class CodingToolHarnessJsonContext : JsonSerializerContext
{
}
```

The context must be part of the resolver chain used by HPD's generated factory path. If the metadata is missing, config resolution fails with a message that names the missing config or metadata type instead of falling back to reflection.

## Provider Tool Schemas

HPD providers consume the same generated or reflected `AIFunctionDeclaration` metadata. A provider should not regenerate function schemas when HPD has already produced them.

For example, ONNX Runtime structured tool calling uses the existing function name, description, and `JsonSchema` from `ChatOptions.Tools`. The ONNX adapter converts those declarations into a compact model-facing tool payload, asks the local model for a constrained JSON tool-call envelope, and returns `FunctionCallContent`. HPD still performs function execution through the normal middleware, permission, event, and result pipeline.

For Native AOT and trimmed apps, this means the same rule applies: keep generated tool registration and JSON metadata available for every config or metadata type HPD hydrates. Local ONNX tool calling does not remove the need for generated schemas; it reuses them.

## Runtime Lookup

`WithToolHarness<T>()` runs the target assembly module initializer, loads the generated registry for that assembly, and resolves the harness by class name.

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness<WeatherTools>()
    .BuildAsync();
```

`WithTool<T>("FunctionName")` uses the generated `FunctionNames` array to validate the requested function:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithTool<WeatherTools>("get_weather")
    .BuildAsync();
```

If the name is wrong, HPD Agent throws an error that lists the available generated functions for that harness.

## Reflection Fallback

If a generated registry entry is unavailable, type-based registration can reflect over simple public harnesses as a convenience in normal JIT development.

Fallback supports public methods with `[AIFunction]`, `[Skill]`, `[SubAgent]`, and `[MultiAgent]`. For instance methods, the harness needs a public parameterless constructor.

Do not rely on fallback for:

- Native AOT.
- Trimmed apps.
- Generated dependency registration.
- Generated metadata/config deserialization.
- Harness-owned MCP/OpenAPI source collection.
- Collapse-scoped middleware factories.

The reflection path is annotated with `RequiresUnreferencedCode` and `RequiresDynamicCode`. It also does not provide the same dependency picture as generated factories: referenced harness and function maps are empty.

## Common Diagnostics

Generator diagnostics to watch for:

- `HPD001`: invalid template property for dynamic descriptions.
- `HPD002`: invalid conditional property.
- `HPD003`: dynamic features used without generic `[AIFunction<TMetadata>]` context.
- `HPD020`: unsupported runtime parameter type; use `FunctionExecutionContext` for sanctioned runtime access.
- `HPDAG0101` and `HPDAG0102`: conflicting literal and expression collapse settings.
- `HPDAG0103`: suspicious string literal used as a collapse expression.
- `HPDAG0201` through `HPDAG0204`: currently reused by multiple generator surfaces. Check the diagnostic title/message to distinguish `[MultiAgent]` diagnostics from `[Collapse(Middlewares = ...)]` diagnostics.
- `HPDAG0301` through `HPDAG0304`: MCP source declaration problems.
- `HPDAG0401` through `HPDAG0403`: OpenAPI source declaration problems.

## Troubleshooting Registry Errors

This error usually means the generated registry did not include the harness:

```text
ToolHarness 'WeatherTools' not found in ToolHarnessRegistry.All.
```

Check the harness shape first:

- The class is public or otherwise publicly accessible from the generated registry.
- The class is instantiable by the generation path you are using.
- The method is public.
- The method has `[AIFunction]` or another supported capability attribute.
- The project references the generator package/source and generated files are being produced.

For Native AOT or trimming work, treat the generated path as mandatory. Reflection fallback is a development convenience, not an AOT strategy. For config and metadata hydration, also register JSON metadata for every user-defined type that HPD hydrates from JSON.

## External Source Guides

- [MCP Servers](mcp-servers.md)
- [OpenAPI Tools](openapi-tools.md)
- [Multi-Agent Capabilities](multi-agent-capabilities.md)
