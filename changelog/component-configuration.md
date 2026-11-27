# Component configuration

This pre-1.0 change gives tool harnesses and middleware one way to declare and receive configuration. It is breaking; there are no compatibility readers or aliases.

## One way to receive configuration

A component declares a config record and takes `IConfig<T>` in its constructor:

```csharp
public sealed record RateLimitConfig
{
    [RunOverridable] public int RequestsPerMinute { get; init; } = 60;
}

[Middleware("rateLimit")]
public sealed class RateLimitMiddleware(IConfig<RateLimitConfig> config) : IAgentMiddleware
{
    public Task BeforeFunctionAsync(BeforeFunctionContext context, CancellationToken cancellationToken)
    {
        var current = config.For(context);
        return Task.CompletedTask;
    }
}
```

- `config.For(context)` returns the run's effective value in any run-scoped hook or tool. `config.AgentDefaults` returns the agent-level value for constructors and start and stop hooks.
- Harnesses, global middleware, and harness middleware all use this. Middleware no longer receives a configuration value in its constructor.
- Config records need no JSON context; the generator emits their codec.
- `[Middleware]` requires a nonempty, case-sensitive identity.

## Configuration

```yaml
toolHarnesses:
  - name: DatabaseHarness
    config:
      maxRows: 500
      middlewares:
        - name: rateLimit
          config: { requestsPerMinute: 30 }
middlewares:
  - name: rateLimit
    config: { requestsPerMinute: 60 }
```

Declaring configurable middleware on a harness adds its configuration to the harness configuration; remove any property you added to the harness record to carry it.

In code: `WithToolHarness<H>(o => o.Configure(config).ConfigureMiddleware<M>(config))` and `WithMiddleware<M>(config)`. Registering an instance of a configurable middleware with `WithMiddleware(instance)` now fails, as does registering it in DI.

## Per-run overrides

Global middleware now accepts per-run overrides, with the same entry shape as harness middleware:

```yaml
middlewares:
  - name: rateLimit
    config: { requestsPerMinute: 20 }
```

```csharp
run.Middlewares.For<RateLimitMiddleware>().Override(new RateLimitConfigOverrides { RequestsPerMinute = 20 });
run.Harnesses.For<DatabaseHarness>().Middleware<RateLimitMiddleware>().Override(new RateLimitConfigOverrides { RequestsPerMinute = 5 });
```

Generated `...Overrides` types contain only `[RunOverridable]` properties, so overriding anything else does not compile. A config record with nothing run-overridable gets no overrides type.

## Renamed

| Before | After |
| --- | --- |
| `IHarnessConfig<T>` | `IConfig<T>` |
| `IHarnessConfigurationContext`, `HarnessConfigurationBinding` | `IConfigContext` (member `ComponentConfig`), `ConfigBinding` |
| `HarnessRunSettingAttribute` | `RunOverridableAttribute` |
| `IHarnessSettingsValidator.ValidateSettings()` | `IConfigValidator.Validate()` |
| `HarnessConfigurationContract`, `HarnessSettingSchema`, `HarnessSettingKind` | `ConfigContract`, `ConfigSchema`, `ConfigKind` |
| `HarnessConfigJson` | `ConfigJson` |
| `HarnessOverrideValue<T>`, `IHarnessOverrides` | `OverrideValue<T>`, `IConfigOverrides` |
| `HookContext.Config` (and runtime and input contexts) | `AgentConfig` |
| `ToolHarnessMiddlewareDescriptor.Settings`, `MiddlewareFactory.Settings` | `Config` |
| Studio middleware `settingsIdentity` | `configIdentity` |

## Removed

- `ToolHarnessReference.MiddlewareConfigs`; a serialized `middlewareConfigs` property is rejected as unknown.
- `[HarnessConfigurationSection]`, `[MiddlewareConfiguration]`, `[ToolHarnessJsonContext]`, and constructor inference from `Config` or `Options` type names.
- `MiddlewareFactory.ConfigType` and `CreateFromConfig`; configurable middleware is built with `CreateWithConfig` and its handle.
- `ToolHarnessActivationContext.GetConfiguration<T>`, `GetConfigurationOrDefault<T>`, `GetHarnessConfiguration<T>`, and `GetMiddlewareConfiguration<T>`.
- `ToolHarnessMiddlewareDescriptor.UsesHarnessConfiguration` and `ConfigurationType`.
- The Coding harness `Environment` and `LanguageServer` properties; configure them as the `coding.environment` and `coding.languageServer` middleware entries.
