# Component Configuration Reference

Tool harnesses and middleware are components. A component may declare one config record and receives it through an `IConfig<T>` handle in its constructor. This reference covers that contract, the configuration shapes, per-run overrides, and diagnostics.

## Public Authoring Types

| Type | Purpose |
| --- | --- |
| `IConfig<T>` | A component's configuration handle: `AgentDefaults` and `For(context)` |
| `IConfigContext` | A context that carries the configuration captured for its run |
| `IConfigValidator` | Optional semantic and cross-field validation |
| `IConfigOverrides` | Base contract for generated run-override types |
| `RunOverridableAttribute` | Marks a property as run-overridable |
| `MiddlewareAttribute` | Declares a middleware's stable configuration identity |
| `ToolHarnessDependencyAttribute` | Declares an agent-owned default dependency |
| `ToolHarnessAgentResourceAttribute` | Declares an agent-lifetime resource available to harness middleware |
| `HarnessRunOverride`, `MiddlewareRunOverride` | Portable serialized run override entries |
| `SubAgentRunConfigs` | Per-input shared and exact direct-child run overrides |
| `SubAgentRunDefaults` | Explicit portable defaults for further descendants |

## Receiving Configuration

Every component receives configuration the same way, whatever its lifetime:

| Component | Lifetime |
| --- | --- |
| Global middleware | Agent runtime, start to stop |
| Tool harness | Agent |
| Harness middleware | Per execution |

```csharp
public sealed record RateLimitConfig
{
    [RunOverridable] public int RequestsPerMinute { get; init; } = 60;
    public int BurstLimit { get; init; } = 10;
}

[Middleware("rateLimit")]
public sealed class RateLimitMiddleware(IConfig<RateLimitConfig> config) : IAgentMiddleware
{
    public Task BeforeStartAsync(BeforeStartContext context, CancellationToken cancellationToken)
    {
        var burst = config.AgentDefaults.BurstLimit;      // no run exists yet
        return Task.CompletedTask;
    }

    public Task BeforeFunctionAsync(BeforeFunctionContext context, CancellationToken cancellationToken)
    {
        var current = config.For(context);                // this run's effective configuration
        return Task.CompletedTask;
    }
}
```

- `config.For(context)` works with any run-scoped context: harness functions, harness activation, permission evaluation, and the input, turn, iteration, tool-execution, function, and error hooks.
- `config.AgentDefaults` is the agent-level value. Use it in constructors and in start and stop hooks, whose contexts carry no run.
- `config.Middleware<TConfig>(context, identity)` reads another middleware in the same scope. A harness uses it to read middleware it declares.
- A component is configurable exactly when its constructor takes `IConfig<T>`. The framework constructs it and supplies the handle, so registering an instance of a configurable middleware fails.
- A middleware class works globally and inside a harness. Declare it on a harness with `[ToolHarnessMiddleware]` or `Collapse.Middlewares`.

## Constructor Contract

A native tool harness has exactly one public constructor. The generator emits a direct activation delegate for the concrete owner.

Constructor parameters may be:

- one `IConfig<T>`;
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

## Config Records

Config records are immutable data contracts with a public parameterless constructor. They need no JSON context; the generator emits their codec.

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
```

The property names are case-sensitive camelCase. `functions` and `metadata` are optional.

### Middleware Configuration

Declaring a configurable middleware on a harness adds its configuration to the harness configuration. The harness config record does not repeat it, and a harness without an `IConfig<T>` constructor parameter still accepts it. Harness middleware entries use the same `name` and `config` shape as global entries:

```yaml
toolHarnesses:
  - name: DatabaseHarness
    config:
      middlewares:
        - name: rateLimit
          config:
            requestsPerMinute: 20
middlewares:
  - name: rateLimit
    config:
      requestsPerMinute: 60
```

`middlewares` is reserved inside a harness `config`. A harness entry configures middleware that the harness already declares; it cannot activate, remove, or reorder middleware. A global entry activates and configures its middleware. Omitted entries and fields keep the generated defaults. Identities are exact and case-sensitive. Unknown, duplicate, or undeclared entries fail.

Each scope owns its own effective configuration, even when the same middleware type is used globally and in several harnesses.

### Builder Registration

```csharp
builder
    .WithToolHarness<DatabaseHarness>(options => options
        .Configure(new DatabaseConfig { MaxRows = 500 })
        .ConfigureMiddleware<RateLimitMiddleware>(new RateLimitConfig { RequestsPerMinute = 20 }))
    .WithMiddleware<RateLimitMiddleware>(new RateLimitConfig { RequestsPerMinute = 60 });
```

`WithMiddleware(instance)` is for middleware without configuration. Builder-registered middleware is added after `AgentConfig` middleware.

## Run Wire Shape

```yaml
harnesses:
  - name: SearchHarness
    config:
      maxResults: 5
    resets:
      - /mode/route
```

Run entries are partial patches over the agent’s effective configuration. `resets` entries use non-root RFC 6901 pointers.

Harness middleware entries are patched by identity, not array position:

```yaml
harnesses:
  - name: DatabaseHarness
    config:
      middlewares:
        - name: rateLimit
          config:
            requestsPerMinute: 5
    resets:
      - /middlewares/rateLimit/requestsPerMinute
```

A patch entry merges into the named middleware's effective configuration and leaves other entries in place. In reset pointers, the identity is one segment; escape `~` as `~0` and `/` as `~1`. Only properties marked `[RunOverridable]` can change per run.

Global middleware uses a top-level `middlewares` list with the same entry shape:

```yaml
middlewares:
  - name: rateLimit
    config:
      requestsPerMinute: 20
    resets:
      - /burstLimit
```

The instance keeps running across runs; only the value `config.For(context)` returns changes.

The run configuration cannot supply constructor dependencies, executable objects, raw configuration handles, middleware resources, or dependency implementation types.

## Generated Override Type

For `SearchConfig`, the library emits `SearchConfigOverrides`. Its properties use `OverrideValue<T>` so an omitted value remains distinguishable from an explicitly supplied default value.

The overrides type contains only what a run may change: properties marked `[RunOverridable]`, every property of a record reached through a marked property, and nested records that contain such properties. Setting a property that is not run-overridable is a compile error. A config record with nothing run-overridable gets no overrides type.

```csharp
runConfig.Harnesses
    .For<SearchHarness>()
    .Override(new SearchConfigOverrides
    {
        MaxResults = 5
    });
```

Middleware configuration uses the middleware's generated override type. The identity comes from the generated definition:

```csharp
runConfig.Harnesses
    .For<DatabaseHarness>()
    .Middleware<RateLimitMiddleware>()
    .Override(new RateLimitConfigOverrides
    {
        RequestsPerMinute = 5
    }, "/burstLimit");
```

Reset pointers passed to `Middleware<T>().Override(...)` are relative to that middleware's configuration.

Global middleware uses `Middlewares.For<T>()`:

```csharp
runConfig.Middlewares
    .For<RateLimitMiddleware>()
    .Override(new RateLimitConfigOverrides { RequestsPerMinute = 20 });
```

## Effective Configuration

```text
library defaults
  → AgentConfig
  → builder (Configure, ConfigureMiddleware, WithMiddleware<T>(config))
  → run override
```

Rules:

- `AgentConfig` entries patch library defaults.
- `.Configure(...)` is a complete value for harness-owned fields. Middleware entries from `AgentConfig` are kept.
- Run overrides patch the effective agent configuration.
- Reset restores the captured library default.
- Collection and dictionary values replace completely.
- Explicit null is valid only for nullable properties.

## Owner Binding

Runtime configuration is bound to:

- one built-agent identity token;
- one exact registered owner: a harness, a middleware in a harness, or a global middleware;
- one accepted execution;
- one immutable effective configuration catalog.

Matching configuration CLR types is insufficient. Wrong-agent and wrong-owner bindings fail with `HarnessConfigurationOwnerMismatch`.

## Diagnostics

| Code | Meaning |
| --- | --- |
| `HarnessNotRegistered` | Run override names no owner in this agent |
| `DuplicateHarness` | Harness or override appears more than once |
| `HarnessConfigUnknownProperty` | Unknown or incorrectly cased property |
| `HarnessConfigDuplicateProperty` | Duplicate property |
| `HarnessConfigInvalidType` | Wrong JSON shape or scalar type |
| `HarnessConfigInvalidValue` | Generated binding failed |
| `HarnessRunOverrideNotSupported` | Property not marked `[RunOverridable]` supplied for a run |
| `HarnessResetInvalid` | Invalid reset path or target |
| `HarnessResetConflict` | Reset overlaps another target |
| `HarnessConfigurationUnbound` | No captured execution binding exists |
| `HarnessConfigurationOwnerMismatch` | Handle belongs to another agent or owner |
| `MiddlewareNotDeclared` | Harness entry names no configurable middleware in this harness |
| `MiddlewareNotRegistered` | Global run entry names no configurable middleware in this agent |
| `DuplicateMiddleware` | Middleware entry or registration appears more than once |
| `middleware_config_not_supported` | Configuration supplied for middleware that does not take `IConfig<T>` |
| `middleware_config_conflict` | A configurable middleware is also registered in DI |

JSON and YAML configuration errors include line and column information.

## Subagent Overrides

`SubAgentRunConfig` entries use the same portable `harnesses` envelope. Entries
are supplied through `subAgentRunConfigs`; each entry must target the shared
baseline or one exact declaration:

```csharp
var childRun = new SubAgentRunConfig
{
    Target = SubAgentTarget.All
};

childRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchConfigOverrides
    {
        MaxResults = 5
    });
```

The child binds the entry against its own registered owner catalog and creates a new runtime configuration binding. Matching the parent's harness type or display name is insufficient.

An exact target overlays the `SubAgentTarget.All` entry field by field only for
that declaration:

```csharp
var reviewerRun = new SubAgentRunConfig
{
    Target = SubAgentTarget.Declared<SearchToolHarness>(
        nameof(SearchToolHarness.Researcher))
};

reviewerRun.Harnesses
    .For<SearchHarness>()
    .Override(new SearchConfigOverrides
    {
        MaxResults = 5
    });
```

Use the generated `.For<T>()` override helper so the harness identity and JSON
shape are generated rather than handwritten.

`SubAgentRunDefaults.Harnesses` carries explicit defaults to further descendants; override layers and descendant defaults carry harness entries only. Parent `AgentRunConfig.Harnesses` never propagates implicitly. Descendant defaults and a more specific child override are applied in that order using the normal patch and reset rules.

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

### Middleware resources

`[ToolHarnessAgentResource(typeof(TImplementation))]` declares a resource
contract for generated harness middleware activation. The generator creates
the implementation with a parameterless constructor. Resources are created
once per built Agent that selects the declaring harness, can be requested by
middleware constructors using the declared contract, and are disposed with the
Agent after its owned harness objects. This is separate from
`[ToolHarnessDependency]`, which supplies a constructor dependency to the
harness itself through the normal dependency resolver.

## Deferred

The current contract does not provide live `Agent` configuration mutation, file watching, resource hot swapping, runtime dependency replacement, capability-graph revision APIs, or implicit subagent harness-override propagation. Explicit subagent overrides are supported.
