# Configure A Harness Per Run

`AgentRunConfig.Harnesses` changes only declared execution settings for one run. The partial override is captured before asynchronous execution and cannot mutate the built agent.

## JSON Or YAML

```yaml
harnesses:
  - name: SearchHarness
    config:
      maxResults: 5
```

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

Supplying `maxResults` preserves every other agent setting:

```text
Agent endpoint       = https://search.internal
Agent maxResults     = 20
Run maxResults       = 5
Effective endpoint   = https://search.internal
Effective maxResults = 5
```

Only properties marked `[HarnessRunSetting]` can be supplied. Construction-only properties fail before execution:

```text
HarnessRunOverrideNotSupported
harnesses[0].config.endpoint
This setting is construction-only.
```

## Typed C#

The generator emits a presence-aware override type from the settings contract:

```csharp
var runConfig = new AgentRunConfig();

runConfig.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 5
    });

await agent.RunAsync(
    "Find the API documentation",
    runConfig: runConfig);
```

Omitted wrapper properties mean “preserve the agent value.” They do not overwrite the setting with the property type’s default.

## Patch Rules

| Input | Result |
| --- | --- |
| Property omitted | Preserve the resolved value |
| Explicit value | Replace that property |
| Explicit null | Assign null only when the setting permits it |
| Nested object | Patch supplied child properties |
| Collection or dictionary | Replace the complete value |
| Reset | Restore the library default |

Unknown fields, duplicate fields, and incorrect case fail with the configuration path.

## Reset To Library Default

```yaml
harnesses:
  - name: SearchHarness
    resets:
      - /maxResults
```

Typed equivalent:

```csharp
runConfig.Harnesses
    .For<SearchHarness>()
    .Override(
        new SearchSettingsOverrides(),
        "/maxResults");
```

A reset restores the library default, not the agent setting:

```text
Library default = 10
Agent setting   = 20
Run reset       = /maxResults
Effective run   = 10
```

Reset pointers use RFC 6901 syntax. A reset cannot overlap a supplied value or another reset on the same path or an ancestor/descendant path.

## Configure Child Harnesses

`SubAgentRunConfig.Harnesses` applies to direct child agents. It uses the same generated override types and patch rules as `AgentRunConfig.Harnesses`, but each named harness is resolved in the invoked child agent's registry:

```csharp
var childRun = new SubAgentRunConfig();

childRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 5
    });

await parentAgent.RunAsync(
    "Research the request",
    subAgentRunConfig: childRun);
```

To give descendants of that child an explicit default, use `SubAgentRunDefaults.Harnesses`:

```csharp
var descendantDefaults = new SubAgentRunDefaults();

descendantDefaults.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 3
    });

var childRun = new SubAgentRunConfig
{
    DescendantDefaults = descendantDefaults
};
```

The same portable shape is available in serialized run configuration:

```yaml
subAgentRunConfig:
  harnesses:
    - name: SearchHarness
      config:
        maxResults: 5
```

Parent `AgentRunConfig.Harnesses` is never forwarded automatically. The child must register the named owner and has its own construction settings, dependencies, middleware, and runtime configuration binding.

## Isolation And Ownership

- Each accepted input captures its own immutable effective snapshot.
- Concurrent runs cannot mutate one another’s harness settings.
- A handle from a different built agent or harness owner is rejected.
- Run overrides do not replace constructor dependencies.
- Run overrides do not recreate shared middleware resources.
- Parent harness overrides do not propagate implicitly to a child; use `subAgentRunConfig` for an explicit child setting.
- `SubAgentRunDefaults.Harnesses` provides explicit defaults for further descendants.
- The internal agent and harness binding is runtime-only and is not serialized.

This API changes harness settings for an execution. It is not a live configuration update for an already built `Agent`.

## Related

- [Configure A Tool Harness](configure-a-tool-harness.md)
- [Tool Harness Configuration Reference](../../reference/harness-configuration.md)
