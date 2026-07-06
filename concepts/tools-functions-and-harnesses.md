# Tools, Functions, And Harnesses

HPD Agent exposes work to a model as functions. A function is a callable `AIFunction` with a name, description, JSON argument schema, execution delegate, and HPD metadata.

A tool is the model-facing callable capability. In native .NET code, a tool is usually a public method marked with `[AIFunction]`. The method becomes an `AIFunction` during agent build.

A tool harness is a class that groups related capabilities. A harness can contain native `[AIFunction]` methods and other capability methods such as skills or subagents. Register a harness with `WithToolHarness<T>()`, or register one generated function from the harness with `WithTool<T>("FunctionName")`.

An external tool source is a loader that contributes functions from outside the native harness method list, such as MCP servers or OpenAPI operations. Those sources still become `AIFunction`s before the model sees them. See [MCP Servers](../guides/tools/mcp-servers.md) and [OpenAPI Tools](../guides/tools/openapi-tools.md).

Client tools are another external execution path: the model sees a normal tool declaration, but the implementation runs in a connected client runtime and returns through bidirectional events. Use them when the capability belongs to the UI, editor extension, desktop app, mobile app, or other SDK client instead of the HPD host process. See [Externally Executed Client Tools](../guides/tools/externally-executed-client-tools.md).

Collapsing is the runtime visibility model that hides a group of functions behind a container tool. A collapsed harness is marked with `[Collapse]`. Before expansion, the model sees the container. After the model calls the container, the member functions become visible on later model iterations.

## Minimal Native Tool

Use `[AIFunction]` on the method. Add `[AIDescription]` to parameters so the generated schema gives the model useful argument guidance.

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

Register the whole harness:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<WeatherTools>()
    .BuildAsync();

var result = await agent.RunAsync("What is the weather in Chicago?");
Console.WriteLine(result.Text);
```

`WithToolHarness<WeatherTools>()` loads the generated registry for the harness assembly, selects the generated `ToolHarnessFactory`, and adds the harness functions to the agent's tool options.

## Attribute Model

`[AIFunction]` is the required attribute for a simple native function. If `Name` is omitted, the method name is used as the generated function name.

`[AIDescription]` is optional but recommended. Use it on parameters and either use `[AIFunction(Description = "...")]` or `[AIDescription("...")]` on the method.

`[AIFunction<TMetadata>]` is for typed metadata scenarios, such as dynamic descriptions or conditional functions. The metadata type must implement `IToolMetadata`.

`[AIFunction(Kind = ToolKind.Output)]` marks an output tool. In that mode, the tool call represents structured output rather than a normal function execution.

`[AIFunction(InvocationModePolicy = ...)]` controls whether a normal function waits for its result or can be launched as runtime-owned background work:

```csharp
[AIFunction(
    Description = "Builds a large search index.",
    InvocationModePolicy = AgentInvocationModePolicy.ModelChoice)]
public async Task<string> BuildIndex(string path, CancellationToken cancellationToken)
{
    await BuildIndexAsync(path, cancellationToken);
    return "Index build completed.";
}
```

Policies:

| Policy | Tool Shape | Tool Result |
| --- | --- | --- |
| `SynchronousOnly` | Original function schema | Final function result |
| `BackgroundOnly` | Original function schema | Background launch receipt |
| `ModelChoice` | Original function schema plus `invocationMode` | Final function result or background launch receipt |

When `ModelChoice` is used, the model can pass `"invocationMode": "background"` to start the work and continue immediately. HPD strips `invocationMode` before binding method parameters, registers runtime-owned background work, and delivers the completion later through [Background Tasks And Notifications](background-tasks-and-notifications.md).

Most functions should use the default `SynchronousOnly`. Use background invocation only when the function can safely continue without the parent model waiting on the call.

`[Collapse]` belongs on the harness class. It is not required for discovery; it only controls container visibility.

## Generated First

The source generator is the normal path. It discovers public, instantiable harnesses and emits a registry under `HPD.Agent.Generated.ToolHarnessRegistry.All`. Generated entries include direct creation delegates, function-name lists, metadata/config deserializers, and collapse metadata.

At runtime, `WithToolHarness<T>()` forces the harness assembly module initializer, loads generated registries, and resolves the harness by class name. If the registry entry is missing for a simple public harness, HPD Agent can use reflection fallback in normal JIT development. Treat that as troubleshooting support, not as the intended production path for Native AOT or trimmed apps.

## Related Guides

- [Author A Tool Harness](../guides/tools/author-a-tool-harness.md)
- [MCP Servers](../guides/tools/mcp-servers.md)
- [OpenAPI Tools](../guides/tools/openapi-tools.md)
- [Externally Executed Client Tools](../guides/tools/externally-executed-client-tools.md)
- [Subagents](../guides/agents/subagents.md)
- [Multi-Agent Capabilities](../guides/tools/multi-agent-capabilities.md)
- [Built-In Harnesses](../guides/harnesses/overview.md)
- [Web Search Harness](../guides/harnesses/web-search.md)
