# Configure A Tool Harness

A configured tool harness receives an immutable, typed settings contract through `IHarnessConfig<T>`. The source generator emits the binding, schema, validation path, JSON codecs, and per-run patch contract from that type.

Use configuration when the whole harness needs behavior settings. Keep executable dependencies in ordinary constructor parameters; see [Inject Harness Dependencies](inject-harness-dependencies.md).

## Declare Settings

```csharp
using HPD.Agent;

public sealed record SearchSettings : IHarnessSettingsValidator
{
    [HarnessRunSetting]
    public int MaxResults { get; init; } = 10;

    public string Endpoint { get; init; } = "https://search.example";

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
    IHarnessConfig<SearchSettings> configuration)
{
    [AIFunction]
    public string Search(string query)
    {
        var settings = configuration.AgentDefaults;
        return $"{query}:{settings.MaxResults}:{settings.Endpoint}";
    }
}
```

The public parameterless constructor supplies the library defaults. Settings properties are init-only and use the supported immutable shapes described below.

`[HarnessRunSetting]` opts a property into the per-run patch contract. Properties without it remain construction-only.

`IHarnessSettingsValidator` is optional. It handles semantic rules that the generated type and shape checks cannot express. Throw `AgentRunConfigurationException` for a stable error code and property path.

## Configure In C#

```csharp
builder.WithToolHarness<SearchHarness>(options => options
    .Configure(new SearchSettings
    {
        MaxResults = 20,
        Endpoint = "https://search.internal"
    }));
```

`.Configure(...)` supplies a complete construction setting. A builder configuration replaces an `AgentConfig` setting for the same harness owner.

## Configure From YAML

```yaml
toolHarnesses:
  - name: SearchHarness
    config:
      maxResults: 20
      endpoint: https://search.internal
```

The equivalent JSON is:

```json
{
  "toolHarnesses": [
    {
      "name": "SearchHarness",
      "config": {
        "maxResults": 20,
        "endpoint": "https://search.internal"
      }
    }
  ]
}
```

String values are not coerced into numbers or booleans. Duplicate and unknown properties fail before the effective configuration is published.

## Precedence

```text
Library defaults
    → AgentConfig document
    → explicit builder Configure
    → per-run override
```

Document settings patch the library defaults. An explicit complete `.Configure(...)` value replaces the document value. Run settings are partial patches; see [Configure A Harness Per Run](configure-a-harness-per-run.md).

## Supported Settings Shape

A settings contract supports:

- strings;
- booleans, integer and floating-point numbers, and `decimal`;
- enums;
- `TimeSpan`, `DateTime`, `DateTimeOffset`, and `Guid`;
- `JsonElement`;
- nested immutable record classes;
- `ImmutableArray<T>` and `ImmutableDictionary<string, T>`.

Settings must have a public parameterless constructor and public init-only properties. Required properties, arrays, mutable collections, and arbitrary object graphs are rejected.

The generator must run in the harness library. A referenced harness package must contain its generated immutable-contract metadata; older packages must be rebuilt.

## Read The Effective Configuration

`configuration.AgentDefaults` is the captured construction setting.

Tools should use `configuration.For(context)` when the harness has run-overridable fields:

```csharp
[AIFunction]
public Task<string> Search(
    string query,
    FunctionExecutionContext context)
{
    var settings = configuration.For(context);
    return SearchAsync(query, settings.MaxResults, settings.Endpoint);
}
```

Resolve the snapshot once. Do not repeatedly read configuration after detaching background work.

## Related

- [Inject Harness Dependencies](inject-harness-dependencies.md)
- [Configure A Harness Per Run](configure-a-harness-per-run.md)
- [Inherit A Tool Harness](inherit-a-tool-harness.md)
- [Tool Harness Configuration Reference](../../reference/harness-configuration.md)
