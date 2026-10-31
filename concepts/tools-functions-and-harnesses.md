# Tools, Functions, And Harnesses

HPD Agent exposes work to a model as functions. A function is a callable `AIFunction` with a name, description, JSON argument schema, execution delegate, and HPD metadata.

A tool is the model-facing callable capability. In native .NET code, a tool is usually a public method marked with `[AIFunction]`. The method becomes an `AIFunction` during agent build.

For a generated native tool, the method is a compiled protocol rather than a method plus runtime inference. HPD emits its closed JSON Schema, strict binder, typed argument carrier, defaults, and invocation adapter from the same signature. See [AI Function Parameter Contracts](../guides/tools/ai-function-parameter-contracts.md).

A tool harness is a class that groups related capabilities. A harness can contain native `[AIFunction]` methods and other capability methods such as skills or subagents. It is also the generated activation contract for its constructor settings, dependencies, middleware, and owned resources. Register a harness with `WithToolHarness<T>()`, or register one generated function from the harness with `WithTool<T>("FunctionName")`.

A skill is a progressive harness capability: the model initially sees its name and description, then receives its instructions and child capabilities only after activation. See [Skills Overview](../guides/skills/overview.md).

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

## Generated Contract First

The source generator is the production path. It discovers public concrete harness owners and emits a registry under `HPD.Agent.Generated.ToolHarnessRegistry.All`. Generated entries include direct activation, DI requirements, immutable settings contracts, function-name lists, metadata codecs, middleware projections, and collapse metadata.

A native harness has exactly one public constructor. Settings are supplied through `IHarnessConfig<T>`; service dependencies are ordinary constructor parameters. Generated activation does not use special parameterless, config-only, or secret-only constructor selection.

For every generated `[AIFunction]`, it also emits:

- a closed model-facing JSON Schema;
- a strict JSON binder;
- a typed argument carrier;
- default and nullability handling;
- nested collection, dictionary, enum, and union binding;
- an invocation adapter that injects runtime-only parameters separately.

Unknown properties, incorrect property casing, invalid enum names, and fields from the wrong union branch fail before the function body executes. The schema and binder come from one generated contract, so they do not drift independently.

At runtime, `WithToolHarness<T>()` forces the harness assembly module initializer, loads the generated registry, and resolves the exact owner type. The generated activation delegate resolves constructor dependencies and creates one agent-owned harness instance. Harness registration does not use reflection fallback.

See [Configure A Tool Harness](../guides/tools/configure-a-tool-harness.md), [Inject Harness Dependencies](../guides/tools/inject-harness-dependencies.md), and [Configure A Harness Per Run](../guides/tools/configure-a-harness-per-run.md).

## Related Guides

- [Author A Tool Harness](../guides/tools/author-a-tool-harness.md)
- [AI Function Parameter Contracts](../guides/tools/ai-function-parameter-contracts.md)
- [Source Generation, AOT, And Trimming](../guides/tools/source-generation-aot-and-trimming.md)
- [MCP Servers](../guides/tools/mcp-servers.md)
- [OpenAPI Tools](../guides/tools/openapi-tools.md)
- [Externally Executed Client Tools](../guides/tools/externally-executed-client-tools.md)
- [Subagents](../guides/agents/subagents.md)
- [Multi-Agent Capabilities](../guides/tools/multi-agent-capabilities.md)
- [Built-In Harnesses](../guides/harnesses/overview.md)
- [Web Search Harness](../guides/harnesses/web-search.md)
