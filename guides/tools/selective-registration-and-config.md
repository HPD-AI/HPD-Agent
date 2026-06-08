# Selective Registration And Config

This is an advanced configuration surface for exposing only part of a harness or constructing a harness from JSON configuration. Start with `WithToolHarness<T>()` unless you need filtering, metadata, constructor config, or scoped middleware config.

## Select One Function In Code

Use `WithTool<T>()` when the model should see one generated function from a harness:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithTool<WeatherTools>("get_weather")
    .BuildAsync();
```

You can also use a qualified name:

```csharp
.WithTool<WeatherTools>("WeatherTools.get_weather")
```

Or a string-only qualified reference:

```csharp
.WithTool("WeatherTools.get_weather")
```

Multiple `WithTool<T>()` calls for the same harness merge the function filters. Unknown function names fail with an error that lists the generated function names available for that harness.

## Register From JSON

`AgentConfig.ToolHarnesses` uses `ToolHarnessReference`. It supports a shorthand string when no extra options are needed:

```json
{
  "ToolHarnesses": [
    "WeatherTools"
  ]
}
```

Use object syntax for selective functions:

```json
{
  "ToolHarnesses": [
    {
      "Name": "WeatherTools",
      "Functions": [ "get_weather" ]
    }
  ]
}
```

`Functions` is `null` by default, which exposes all generated functions for the harness.

Function filters are source-confirmed, but the config path stores filters without validating names at config-resolution time. Strong early validation is confirmed for builder `.WithTool<T>()`.

## Constructor Config

If a harness has a supported single config constructor, generated code can deserialize `Config` and pass it to that constructor:

```json
{
  "ToolHarnesses": [
    {
      "Name": "SearchTools",
      "Config": {
        "MaxResults": 5
      }
    }
  ]
}
```

The generated factory owns this behavior. The config type is detected during generation, and `CreateFromConfig` is used during tool-harness resolution.

The harness can be defined in a referenced library. The library that defines the harness carries its generated `ToolHarnessRegistry`, and the application can resolve that harness once the library assembly has been loaded. The JSON config type still needs source-generated JSON metadata available to HPD at runtime.

For a package-owned harness, keep the config metadata with the package:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(SearchToolsConfig))]
public partial class SearchToolsJsonContext : JsonSerializerContext
{
}
```

HPD uses the generated factory path for config hydration. If the config metadata is not registered in the resolver chain, config resolution fails with an error naming the missing config type.

## Typed Metadata

Use `Metadata` when functions use generic capability attributes such as `[AIFunction<TMetadata>]` for dynamic descriptions or conditional logic.

```json
{
  "ToolHarnesses": [
    {
      "Name": "SearchTools",
      "Metadata": {
        "DefaultProvider": "tavily",
        "HasTavilyProvider": true
      }
    }
  ]
}
```

The generated `DeserializeMetadata` delegate deserializes the JSON to the metadata type detected from the harness's generic capability attributes.

Typed metadata follows the same JSON metadata contract as constructor config. If a metadata type is hydrated from `ToolHarnessReference.Metadata`, include it in a source-generated JSON context and make that context available to HPD's resolver chain.

## Scoped Middleware Config

Collapsed harnesses can declare scoped middleware:

```csharp
[Collapse("Database operations", Middlewares = [typeof(DbAuditMiddleware)])]
public sealed class DatabaseTools
{
    [AIFunction]
    public string Query(string sql) => "result";
}
```

If a scoped middleware has a supported single `*Config` or `*Options` constructor, `MiddlewareConfigs` can provide its config:

```json
{
  "ToolHarnesses": [
    {
      "Name": "DatabaseTools",
      "MiddlewareConfigs": {
        "DbAuditMiddleware": {
          "AuditLevel": "Detailed"
        }
      }
    }
  ]
}
```

For middleware that needs dependency injection or non-config constructor arguments, configure it in code:

```csharp
builder.WithToolHarness<DatabaseTools>(options =>
    options.AddScopedMiddleware(new DbAuditMiddleware(auditLog)));
```

Scoped middleware pipelines are instantiated when the container expands and apply to functions owned by that harness.

Middleware config values use generated config factories. A middleware config type supplied through `MiddlewareConfigs` needs source-generated JSON metadata just like a tool harness constructor config type.

## Builder And Config Composition

Config tool harnesses are resolved before functions are created. Builder registrations can override or skip config entries when the same harness is registered through code. `WithToolHarnessOverride(ToolHarnessReference)` exists for explicit config-plus-builder composition.

For Native AOT and trimmed apps, use generated harnesses and generated JSON metadata together: registry generation makes the harness discoverable, and JSON metadata makes config and metadata hydration reflection-free.
