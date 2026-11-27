# Selective Registration And Config

This page covers function selection and the serialized harness reference. Configuration, dependencies, and run overrides have focused guides:

- [Configure A Tool Harness](configure-a-tool-harness.md)
- [Inject Harness Dependencies](inject-harness-dependencies.md)
- [Configure A Harness Per Run](configure-a-harness-per-run.md)
- [Tool Harness Configuration Reference](../../reference/harness-configuration.md)

## Select One Function

Use `WithTool<T>()` when the model should see one generated function:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithTool<WeatherTools>("get_weather")
    .BuildAsync();
```

Qualified references are also supported:

```csharp
.WithTool<WeatherTools>("WeatherTools.get_weather")
```

Multiple registrations for the same harness merge their function filters. Unknown names fail with the generated function list.

## Serialized Harness Reference

`AgentConfig.ToolHarnesses` supports the shorthand string form:

```json
{
  "toolHarnesses": [
    "WeatherTools"
  ]
}
```

Use object syntax to select functions:

```json
{
  "toolHarnesses": [
    {
      "name": "WeatherTools",
      "functions": [
        "get_weather"
      ]
    }
  ]
}
```

`functions: null` exposes every generated function.

Property names are case-sensitive camelCase. Unknown and duplicate reference properties fail instead of being skipped.

## Serialized Construction Config

```yaml
toolHarnesses:
  - name: SearchHarness
    config:
      maxResults: 20
      endpoint: https://search.internal
```

The registered harness library binds `config` through its generated immutable settings codec. Config fields are patched over library defaults.

See [Configure A Tool Harness](configure-a-tool-harness.md) for C#, YAML, precedence, and validation.

## Run Config

Per-run settings use a separate `harnesses` property:

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

This is a partial execution override, not another construction document. See [Configure A Harness Per Run](configure-a-harness-per-run.md).

## Typed Metadata

Generic metadata remains separate from construction settings:

```json
{
  "toolHarnesses": [
    {
      "name": "SearchHarness",
      "metadata": {
        "defaultProvider": "tavily",
        "hasTavilyProvider": true
      }
    }
  ]
}
```

The harness must declare a compatible metadata contract. Metadata affects capability descriptions, visibility, and conditions; it does not provide constructor settings or dependencies.

## Middleware Configuration

Settings for middleware a harness declares go in the harness `config`, addressed by the middleware's identity:

```yaml
toolHarnesses:
  - name: DatabaseToolHarness
    config:
      middlewares:
        - name: databaseRateLimit
          config:
            requestsPerMinute: 20
```

The entry configures declared middleware only; it does not activate or reorder it. Harness-owned settings sit beside `middlewares` in the same `config` object.

## Composition

- A builder-selected harness and its serialized reference resolve to one exact owner.
- A builder `.Configure(...)` replaces serialized harness-owned settings. Serialized `middlewares` entries are kept.
- A run override patches the effective agent setting.
- Duplicate harness entries and duplicate builder owner registrations fail.
- Explicit instances retain their existing settings and dependencies.

## Native AOT

The harness library's source generator emits the activation and settings codecs. Keep the generator on in the library project and rebuild referenced harness packages when adopting this contract.

No application-side copy of the harness settings type is required.
