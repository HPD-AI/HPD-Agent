# Tool Harness Configuration Reference

This reference describes the common configuration and activation contract used by native tool harnesses.

## Public Authoring Types

| Type | Purpose |
| --- | --- |
| `IHarnessConfig<T>` | Immutable construction and execution settings handle |
| `IHarnessSettingsValidator` | Optional semantic and cross-field validation |
| `IHarnessOverrides` | Base contract for generated run-override types |
| `HarnessRunSettingAttribute` | Marks a property as run-overridable |
| `HarnessConfigurationSectionAttribute` | Projects a harness settings section into middleware |
| `ToolHarnessDependencyAttribute` | Declares an agent-owned default dependency |
| `HarnessRunOverride` | Portable serialized run override entry |
| `SubAgentRunConfig` | Per-input run overrides for direct child agents |
| `SubAgentRunDefaults` | Explicit portable defaults for further descendants |

## Constructor Contract

A native tool harness has exactly one public constructor. The generator emits a direct activation delegate for the concrete owner.

Constructor parameters may be:

- one `IHarnessConfig<T>`;
- service dependencies resolved from the precedence below;
- optional parameters with valid defaults.

Precedence:

```text
registration UseDependency
    → application DI
    → ToolHarnessDependency default
    → missing-dependency error
```

The generic `WithToolHarness<T>()` registration does not impose a `new()` constraint.

## Settings Contract

Settings are immutable data contracts with a public parameterless constructor.

Supported values:

```text
string
bool
signed and unsigned integers
float, double, decimal
enum
TimeSpan, DateTime, DateTimeOffset, Guid
JsonElement
nested immutable record class
ImmutableArray<T>
ImmutableDictionary<string, T>
```

Properties are public and init-only. Required members are rejected. Arrays, mutable collections, and arbitrary object graphs are rejected.

## Construction Wire Shape

```yaml
toolHarnesses:
  - name: SearchHarness
    functions:
      - search
    config:
      maxResults: 20
    metadata:
      provider: internal
    middlewareConfigs:
      LegacyMiddleware:
        enabled: true
```

The property names are case-sensitive camelCase. `functions`, `metadata`, and `middlewareConfigs` are optional.

`middlewareConfigs` is the compatibility path for middleware outside a typed harness settings section. A typed section and a legacy config for the same middleware conflict.

## Run Wire Shape

```yaml
harnesses:
  - name: SearchHarness
    config:
      maxResults: 5
    resets:
      - /mode/route
```

Run entries are partial patches over the agent’s effective settings. `resets` entries use non-root RFC 6901 pointers.

The run configuration cannot supply constructor dependencies, executable objects, raw configuration handles, middleware resources, or dependency implementation types.

## Generated Override Type

For `SearchSettings`, the library emits `SearchSettingsOverrides`. Its properties use `HarnessOverrideValue<T>` so an omitted value remains distinguishable from an explicitly supplied default value.

```csharp
runConfig.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 5
    });
```

## Effective Configuration

```text
library defaults
  → AgentConfig settings
  → builder Configure
  → run override
```

Rules:

- `AgentConfig` settings patch library defaults.
- `.Configure(...)` is a complete construction setting.
- Run overrides patch the effective agent setting.
- Reset restores the captured library default.
- Collection and dictionary values replace completely.
- Explicit null is valid only for nullable properties.

## Owner Binding

Runtime configuration is bound to:

- one built-agent identity token;
- one exact registered harness owner;
- one accepted execution;
- one immutable effective settings catalog.

Matching settings CLR types is insufficient. Wrong-agent and wrong-owner bindings fail with `HarnessConfigurationOwnerMismatch`.

## Diagnostics

| Code | Meaning |
| --- | --- |
| `HarnessNotRegistered` | Run override names no owner in this agent |
| `DuplicateHarness` | Harness or override appears more than once |
| `HarnessConfigUnknownProperty` | Unknown or incorrectly cased setting |
| `HarnessConfigDuplicateProperty` | Duplicate setting |
| `HarnessConfigInvalidType` | Wrong JSON shape or scalar type |
| `HarnessConfigInvalidValue` | Generated binding failed |
| `HarnessRunOverrideNotSupported` | Construction-only setting supplied for a run |
| `HarnessResetInvalid` | Invalid reset path or target |
| `HarnessResetConflict` | Reset overlaps another target |
| `HarnessConfigurationUnbound` | No captured execution binding exists |
| `HarnessConfigurationOwnerMismatch` | Handle belongs to another agent or owner |
| `HarnessConfigurationConflict` | Typed section and legacy middleware config conflict |

JSON and YAML configuration errors include line and column information.

## Subagent Overrides

`SubAgentRunConfig` uses the same portable `harnesses` envelope for direct children:

```csharp
var childRun = new SubAgentRunConfig();

childRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 5
    });
```

The child binds the entry against its own registered owner catalog and creates a new runtime configuration binding. Matching the parent's harness type or display name is insufficient.

`SubAgentRunDefaults.Harnesses` carries explicit defaults to further descendants. Parent `AgentRunConfig.Harnesses` never propagates implicitly. Descendant defaults and a more specific child override are applied in that order using the normal patch and reset rules.

Runtime configuration handles, built-agent tokens, dependency instances, middleware instances, and object graphs are never propagated.

## Ownership

| Object | Owner |
| --- | --- |
| Generated harness | Built `Agent` |
| Dependency created by HPD default | Built `Agent` |
| `UseDependency` instance | Caller |
| DI-created dependency | DI container |
| Explicit harness instance | Caller |

Agent-owned harness objects are released before agent-owned resources.

## Deferred

The current contract does not provide live `Agent` configuration mutation, file watching, resource hot swapping, runtime dependency replacement, capability-graph revision APIs, or implicit subagent harness-override propagation. Explicit subagent overrides are supported.
