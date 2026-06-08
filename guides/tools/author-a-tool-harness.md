# Author A Tool Harness

A tool harness is a public class that groups methods HPD Agent can expose to the model. The smallest useful harness is a parameterless class with public `[AIFunction]` methods.

## Create The Harness

```csharp
using HPD.Agent;

public sealed class WeatherTools
{
    [AIFunction(Description = "Gets the current weather for a city.")]
    public string GetWeather(
        [AIDescription("The city to get weather for.")] string city)
    {
        return $"It is sunny in {city}.";
    }
}
```

`[AIFunction]` makes `GetWeather` callable. The generated function name is `GetWeather` unless you set `Name`.

Use explicit names when you want stable snake_case tool names:

```csharp
public sealed class WeatherTools
{
    [AIFunction(Name = "get_weather")]
    [AIDescription("Gets the current weather for a location.")]
    public string GetWeather(
        [AIDescription("The city or location to get weather for.")] string location)
    {
        return $"It is sunny in {location}.";
    }
}
```

## Register The Harness

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("Use the weather tool when the user asks about weather.")
    .WithToolHarness<WeatherTools>()
    .BuildAsync();

var result = await agent.RunAsync("What is the weather in Chicago?");
Console.WriteLine(result.Text);
```

`WithToolHarness<WeatherTools>()` registers all generated functions from the harness.

## Runtime Context

Tools can accept `FunctionExecutionContext` when they need runtime-only access during execution:

```csharp
using HPD.Agent;
using HPD.Agent.Middleware;

public sealed record SearchProgressEvent(string Query, string Stage) : AgentEvent;

public sealed class SearchTools
{
    [AIFunction(Name = "search_documents")]
    public async Task<string> SearchDocuments(
        string query,
        FunctionExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Emit(new SearchProgressEvent(query, "started"));

        var result = await SearchAsync(query, cancellationToken);

        context.Emit(new SearchProgressEvent(query, "completed"));
        return result;
    }
}
```

`FunctionExecutionContext` and `CancellationToken` are runtime parameters. They can appear in the C# method signature, but they are supplied by HPD Agent and excluded from the generated tool schema and argument DTO. The model does not see them and cannot provide values for them; it only sees the real tool arguments.

Use the context for sanctioned runtime access such as emitting events, reading call metadata, accessing services, using the content store, emitting process-local struct samples, registering background tasks, or asking bidirectional questions through `RequestAsync(...)`. For tool progress, bidirectional requests, struct event caveats, and background task events, see [Tool And Function Events](../events/tool-and-function-events.md).

To register one generated function, use `WithTool<T>()`:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithTool<WeatherTools>("get_weather")
    .BuildAsync();
```

`WithTool<T>()` validates the name against the generated `FunctionNames` list. You can also use a qualified reference:

```csharp
.WithTool<WeatherTools>("WeatherTools.get_weather")
```

If the harness name in the qualified reference does not match `T`, build configuration fails early.

## Constructor Rules

The common generic overload is:

```csharp
.WithToolHarness<WeatherTools>()
```

This overload requires `WeatherTools` to satisfy `where T : class, new()`, so it needs a public parameterless constructor.

For a pre-created instance, use:

```csharp
var tools = new WeatherTools();

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithToolHarness(tools)
    .BuildAsync();
```

Instance registration still relies on generated factory metadata for function creation. If the generated registry is unavailable, this path does not use the reflection fallback used by the type-based overloads.

For JSON-configured harnesses, use a single public config constructor:

```csharp
public sealed class SearchTools
{
    public SearchTools(SearchToolsConfig config)
    {
        Config = config;
    }

    public SearchToolsConfig Config { get; }

    [AIFunction]
    public string Search(string query) => query;
}

public sealed class SearchToolsConfig
{
    public int MaxResults { get; set; } = 5;
}
```

The generated registry includes public harnesses that can be constructed through a parameterless constructor, a single config constructor, or an `ISecretResolver` constructor.

If `SearchToolsConfig` is hydrated from JSON, it needs source-generated JSON metadata:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(SearchToolsConfig))]
public partial class SearchToolsJsonContext : JsonSerializerContext
{
}
```

The harness can live in a referenced library. That library owns the generated `ToolHarnessRegistry` for the harness, and it should also own JSON metadata for config and metadata types that it expects HPD to hydrate.

## Generated Registry Behavior

The generator discovers classes that contain capability methods such as `[AIFunction]`, `[Skill]`, `[SubAgent]`, `[MCPServer]`, or `[OpenApi]`, and classes marked with `[Collapse]`.

Generated output includes:

- One factory entry per supported harness.
- The generated function-name array used by `WithTool<T>()`.
- Direct delegates for creating functions.
- Metadata and config deserializers when the harness uses those features.
- Collapse metadata for container visibility.

Only publicly accessible, instantiable harnesses are included in `ToolHarnessRegistry.All`. If registration fails with a message like `ToolHarness 'WeatherTools' not found in ToolHarnessRegistry.All`, check that the class is public, the method is public, `[AIFunction]` is present, the constructor shape is supported, and source generation ran for the assembly.

## Reflection Fallback

For type-based registration, HPD Agent can reflect over simple public harnesses when a generated registry entry is missing. The fallback supports public methods with `[AIFunction]`, `[Skill]`, `[SubAgent]`, and `[MultiAgent]`.

The non-generic `WithToolHarness(Type)` path can use reflection fallback for simple public harnesses in JIT development. Generic overloads still have their normal constructor constraints, and instance registration still relies on generated factory metadata.

Do not rely on reflection fallback for Native AOT, trimmed apps, generated dependency registration, external MCP/OpenAPI harness sources, collapse-scoped middleware factories, or generated config/metadata deserialization. The generated path uses registry entries and JSON metadata; reflection fallback is annotated with trimming and dynamic-code warnings and does not provide the full generated dependency picture.
