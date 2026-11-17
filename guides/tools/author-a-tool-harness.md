# Author A Tool Harness

A tool harness is a public class that groups model-callable capabilities. It has exactly one public constructor and is registered with `WithToolHarness<T>()`.

The source generator emits direct activation, function schemas and binders, settings codecs, middleware projections, ownership metadata, and Native AOT-safe delegates.

## Minimal Harness

```csharp
using HPD.Agent;

public sealed class WeatherTools
{
    [AIFunction(Name = "get_weather")]
    [AIDescription("Gets the current weather for a location.")]
    public string GetWeather(
        [AIDescription("The city or location.")] string location)
        => $"It is sunny in {location}.";
}
```

The implicit public parameterless constructor is valid because this harness needs no services or settings.

Register the whole harness:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("Use the weather tool when the user asks about weather.")
    .WithToolHarness<WeatherTools>()
    .BuildAsync();
```

To expose one function instead, use `WithTool<WeatherTools>("get_weather")`.
This selects that generated function as the harness's tool entry point. Use
`WithToolHarness<T>()` to register the harness and its generated capability
catalog. For the selection behavior and supported combinations, see
[Selective Registration](selective-registration-and-config.md).

## Add Configuration

Configuration is a separate immutable contract:

```csharp
public sealed record WeatherSettings : IHarnessSettingsValidator
{
    [HarnessRunSetting]
    public int ResultLimit { get; init; } = 5;

    public string Units { get; init; } = "metric";

    public void ValidateSettings()
    {
        if (ResultLimit < 1)
            throw new AgentRunConfigurationException(
                "WeatherSettingsInvalid",
                "config.resultLimit",
                "ResultLimit must be positive.");
    }
}

public sealed class WeatherTools(
    IHarnessConfig<WeatherSettings> configuration)
{
    [AIFunction]
    public string GetWeather(
        string location,
        FunctionExecutionContext context)
    {
        var settings = configuration.For(context);
        return $"Weather for {location}: {settings.ResultLimit} results in {settings.Units}.";
    }
}
```

`configuration.AgentDefaults` is the construction setting. `configuration.For(context)` returns the effective setting for the current execution.

`[HarnessRunSetting]` opts a property into per-run overrides. Unmarked properties are construction-only.

See [Configure A Tool Harness](configure-a-tool-harness.md) and [Configure A Harness Per Run](configure-a-harness-per-run.md).

## Add Dependencies

Dependencies are ordinary constructor parameters:

```csharp
public sealed class WeatherTools(
    IHarnessConfig<WeatherSettings> configuration,
    IWeatherClient client)
{
}
```

Consumers can use application DI:

```csharp
builder.WithToolHarness<WeatherTools>();
```

Or override a dependency for this registration:

```csharp
builder.WithToolHarness<WeatherTools>(options => options
    .Configure(new WeatherSettings { Units = "imperial" })
    .UseDependency<IWeatherClient>(customClient));
```

See [Inject Harness Dependencies](inject-harness-dependencies.md).

## Runtime Parameters

`FunctionExecutionContext` and `CancellationToken` are supplied by HPD Agent and excluded from the model-facing function schema:

```csharp
[AIFunction]
public async Task<string> SearchDocuments(
    string query,
    FunctionExecutionContext context,
    CancellationToken cancellationToken)
{
    await context.PublishAsync(
        new SearchStartedEvent(query),
        cancellationToken);

    return await SearchAsync(query, cancellationToken);
}
```

Use the context for sanctioned runtime access such as events, services, content, runtime-owned operations, and bidirectional requests.

## Pass Application Data For One Run

Use `AgentRunConfig.Context.Properties` to pass named values from the caller
to a tool or middleware for one run. The consumer defines the key and the
expected value type. Use [per-run harness configuration](configure-a-harness-per-run.md)
to override declared harness settings.

```csharp
var runConfig = new AgentRunConfig
{
    Context = new AgentContextRunConfig
    {
        Properties = new Dictionary<string, object>
        {
            ["search.collection"] = "product-docs",
        },
    },
};

var result = await agent.RunAsync("Find the setup instructions.", runConfig: runConfig);
```

Inside a tool function that takes a `FunctionExecutionContext context`
parameter, read the value by key and handle a missing or unexpected value.

```csharp
var properties = context.RunConfig.Context?.Properties;
var collection = properties is not null &&
    properties.TryGetValue("search.collection", out var value) &&
    value is string name
        ? name
        : "general-docs";
```

HPD captures context when the run starts. Supported values include strings,
numbers, JSON values, and supported collections. Custom values can implement
`IAgentImmutableContextValue` or `IAgentContextSnapshotValue`. The host can
also explicitly share concurrency-safe values by listing their keys in
`AgentContextRunConfig.SharedProperties`.

See [Custom Middleware](../middleware/custom-middleware.md#read-caller-supplied-run-context)
for reading the same data in hooks and the [Coding harness](../harnesses/coding.md#provide-a-workspace)
for an example using a structured value.

## Constructor Rules

- A harness has exactly one public constructor.
- Settings are accepted through `IHarnessConfig<T>`.
- Services use normal dependency parameters.
- The generic registration no longer requires `new()`.
- A missing public constructor produces `HPDHIN009`; multiple public constructors produce `HPDHIN010`.
- There is no constructor-selection attribute or compatibility overload path.

## Explicit Instances

```csharp
var tools = new WeatherTools(configurationHandle, client);
builder.WithToolHarness(tools);
```

The supplied harness and its captured dependencies retain their current state. Registration-time configuration and dependency customization are rejected for an explicit instance.

## Shared Agent-owned resources

Harness middleware can use a resource shared for the lifetime of the built
Agent. Declare the resource on its contract with
`[ToolHarnessAgentResource(typeof(ResourceImplementation))]`, then accept the
contract in a declared middleware constructor. HPD generates resource creation
and cleanup. The implementation must have an accessible parameterless
constructor. See [Agent-owned resources in the middleware guide](../middleware/tool-harness-middleware.md#agent-owned-resources)
for a complete example and lifecycle details.

## Generated Contract

The generator discovers public harness types containing capabilities such as `[AIFunction]`, `[Skill]`, `[SubAgent]`, `[McpServer]`, or `[OpenApi]`.

Generated output includes:

- The assembly's `ToolHarnessRegistry.All` catalog.
- Direct activation for the concrete owner.
- Direct function creation delegates.
- Closed JSON Schemas and strict argument binders.
- Runtime parameter injection.
- Typed metadata and settings support.
- Immutable settings schemas, codecs, validators, and run-override types.
- Middleware section projections.
- External source collectors and collapse metadata.
- Native AOT registration.

The project defining the harness must reference the source generator. A referenced library owns its generated registry and configuration codecs.

## Troubleshooting

`HPDHIN009` means the harness has no public constructor. `HPDHIN010` means it has more than one; generated activation requires exactly one.

`HPDCFG001` means the settings type contains an unsupported mutable or unverifiable shape.

`HPDCFG002` means a middleware section projection does not match the owning immutable settings contract.

`ToolHarness '...' not found in ToolHarnessRegistry.All` usually means the harness is not publicly accessible, has no recognized capability, or was compiled without the source generator.

For progressive instruction-and-capability workflows, continue with [Skills Overview](../skills/overview.md).
