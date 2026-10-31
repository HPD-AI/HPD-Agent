# Tool Harness Configuration And Activation

This pre-1.0 change replaces the fragmented native harness constructor, configuration, and activation paths with one generated owner contract.

## Breaking Changes

- A native harness now has exactly one public constructor.
- `WithToolHarness<T>()` no longer requires `where T : class, new()`.
- Harness registration no longer silently selects a preconstructed harness from DI.
- Harness registration no longer uses reflection fallback.
- A configured harness accepts `IHarnessConfig<T>` instead of a constructor parameter whose type merely ends in `Config`.
- Library dependency defaults use `[ToolHarnessDependency]`.
- Executable dependency defaults and explicit instances have explicit ownership.
- Configuration and metadata remain separate contracts.
- External harness libraries must be rebuilt with the current source generator.
- `SubAgentRunConfig.Harnesses` now configures direct child agents and survives input snapshots, durable policy, and continuation.
- `SubAgentRunDefaults.Harnesses` explicitly propagates harness defaults to further descendants.

## Before

```csharp
public sealed class SearchHarness
{
    public SearchHarness(SearchConfig config)
    {
    }
}
```

```yaml
toolharnesses:
  - name: SearchHarness
    config:
      maxResults: 20
```

There was no standard harness run-settings contract. Dependency-heavy harnesses were typically constructed by application code or resolved from DI as complete objects.

## After

```csharp
public sealed record SearchSettings : IHarnessSettingsValidator
{
    [HarnessRunSetting]
    public int MaxResults { get; init; } = 10;

    public void ValidateSettings()
    {
        if (MaxResults < 1)
            throw new AgentRunConfigurationException(
                "SearchSettingsInvalid",
                "config.maxResults",
                "MaxResults must be positive.");
    }
}

public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
}
```

```csharp
builder.WithToolHarness<SearchHarness>(options => options
    .Configure(new SearchSettings { MaxResults = 20 })
    .UseDependency<ISearchClient>(customClient));
```

```yaml
toolHarnesses:
  - name: SearchHarness
    config:
      maxResults: 20
harnesses:
  - name: SearchHarness
    config:
      maxResults: 5
```

The construction property is `toolHarnesses`. The per-run property is `harnesses`.

Direct child and descendant overrides use the same portable envelope:

```csharp
var childRun = new SubAgentRunConfig();
childRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides { MaxResults = 5 });

var descendantDefaults = new SubAgentRunDefaults();
descendantDefaults.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides { MaxResults = 3 });
childRun.DescendantDefaults = descendantDefaults;
```

Parent harness overrides remain isolated and never propagate implicitly.

## Migration Checklist

- Reduce the harness to one public constructor.
- Replace broad mutable options with an immutable settings record.
- Inject the settings through `IHarnessConfig<T>`.
- Mark only run-safe properties with `[HarnessRunSetting]`.
- Implement `IHarnessSettingsValidator` when cross-field validation is required.
- Keep services as ordinary constructor dependencies.
- Use `[ToolHarnessDependency]` only for concrete library defaults.
- Update JSON/YAML property casing and the legacy constructor config shape.
- Replace `AddScopedMiddleware(...)` with declared middleware and exact-type override activation.
- Rebuild packages containing harnesses.

See:

- [Configure A Tool Harness](../guides/tools/configure-a-tool-harness.md)
- [Inject Harness Dependencies](../guides/tools/inject-harness-dependencies.md)
- [Configure A Harness Per Run](../guides/tools/configure-a-harness-per-run.md)
- [Inherit A Tool Harness](../guides/tools/inherit-a-tool-harness.md)
