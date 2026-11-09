# Inject Harness Dependencies

Tool harness dependencies use ordinary constructor parameters. The source generator emits direct activation code that resolves those parameters without reflection.

## One Public Constructor

```csharp
public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
    [AIFunction]
    public Task<string> Search(string query)
        => client.SearchAsync(query, configuration.AgentDefaults.MaxResults);
}
```

A harness has exactly one public constructor. It may mix:

- one `IHarnessConfig<T>` settings parameter;
- ordinary service parameters;
- optional parameters with valid compile-time defaults.

Generated activation does not infer special parameterless, config-only, or `ISecretResolver`-only constructor shapes. The single public constructor is the activation contract.

## Resolve A Dependency

Dependencies resolve in this order:

```text
Registration-local UseDependency
    → application DI
    → library-declared default
    → missing-dependency error
```

Override one service for this harness registration:

```csharp
builder.WithToolHarness<SearchHarness>(options => options
    .UseDependency<ISearchClient>(customClient));
```

The supplied instance is borrowed. The application remains responsible for its lifetime.

`UseDependency(...)` affects constructor activation. It does not populate typed metadata for capabilities such as `[AIFunction<TMetadata>]`. Supply metadata through the registration context argument when the harness needs both:

```csharp
builder.WithToolHarness<SearchHarness>(
    options => options.UseDependency<ISearchClient>(customClient),
    metadata);
```

## Use Application DI

```csharp
services.AddSingleton<ISearchClient, CustomSearchClient>();

builder
    .WithServiceProvider(serviceProvider)
    .WithToolHarness<SearchHarness>();
```

Objects created by DI belong to the DI container. HPD does not dispose them.

## Declare A Library Default

```csharp
[ToolHarnessDependency(typeof(ISearchClient), typeof(SearchClient))]
public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
}
```

`SearchClient` also follows the one-public-constructor rule. HPD owns and disposes defaults it creates.

Two declarations for the same service contract are a configuration conflict. A derived harness does not currently replace a base default implicitly.

## Explicit Harness Instances

```csharp
var harness = new SearchHarness(configurationHandle, client);
builder.WithToolHarness(harness);
```

An explicit instance uses its current dependencies and captured state. Registration-time `.Configure(...)` and `.UseDependency(...)` are rejected for that instance. The caller owns it.

Explicit-instance registration validates immediately:

- A null instance throws `ArgumentNullException`.
- Registering the same harness instance slot twice throws
  `InvalidOperationException`.
- A harness without a generated registration fails before the agent is built.

Instance registration is builder state. It is not exposed as a mutable public
collection; use `WithToolHarness(instance)` and optional registration metadata
through the context argument.

## Lifetime Rules

| Object | Owner |
| --- | --- |
| Generated harness | Built `Agent` |
| Dependency created from `[ToolHarnessDependency]` | Built `Agent` |
| `.UseDependency(...)` instance | Caller |
| DI-created service | DI container |
| Explicit harness instance | Caller |

An agent-owned harness constructor must not capture a dependency that requires an execution scope. Use execution-scoped middleware or the execution resource contracts when behavior belongs to one accepted run.

## Related

- [Configure A Tool Harness](configure-a-tool-harness.md)
- [Inherit A Tool Harness](inherit-a-tool-harness.md)
- [Tool Harness Configuration Reference](../../reference/harness-configuration.md)
