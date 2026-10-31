# Inherit A Tool Harness

A derived harness inherits capabilities from its base and is registered as one concrete owner. The generator activates the derived type, composes inherited middleware and instructions, and binds inherited tools to that same owner.

## Define The Derived Harness

```csharp
public sealed record StrategySearchSettings : SearchSettings
{
    [HarnessRunSetting]
    public bool PreferRecent { get; init; } = true;
}

public sealed class StrategySearchHarness(
    IHarnessConfig<StrategySearchSettings> configuration,
    ISearchClient client)
    : SearchHarness(configuration, client)
{
    [AIFunction]
    public string Strategy(string topic) => $"Strategy: {topic}";
}
```

Constructors are ordinary C# forwarding. There is no constructor-selection attribute.

Register only the concrete derived owner:

```csharp
builder.WithToolHarness<StrategySearchHarness>();
```

The inherited `SearchHarness` tools and the derived `Strategy` tool execute under the `StrategySearchHarness` identity. The base harness is not registered as a second owner.

## Shared Configuration View

`IHarnessConfig<T>` is covariant. A base constructor can receive `IHarnessConfig<StrategySearchSettings>` as `IHarnessConfig<SearchSettings>`.

Both views resolve the same captured concrete-owner snapshot:

```text
Concrete owner:
  StrategySearchHarness

Settings snapshot:
  StrategySearchSettings
    ├── inherited SearchSettings fields
    └── derived PreferRecent field

Consumers:
  inherited tools
  derived tools
  inherited middleware
  derived middleware
```

A base view cannot use settings belonging to a separately registered base owner.

## Run Overrides

Override the concrete owner:

```csharp
runConfig.Harnesses
    .For<StrategySearchHarness>()
    .Override(new StrategySearchSettingsOverrides
    {
        MaxResults = 5,
        PreferRecent = true
    });
```

The inherited base view receives the same `MaxResults` value.

## Inheritance Rules

- The concrete harness has exactly one public constructor.
- Public inherited capabilities are included once.
- Virtual overrides remain represented by one capability slot.
- Explicit replacements and exclusions follow the inheritance generator diagnostics.
- Base `[Collapse]` instructions come before derived instructions.
- Base middleware comes before derived additions.
- An exact middleware type repeated in the derived declaration keeps its inherited position.
- Base and derived registrations are independent owners only when both concrete types are registered separately.

## Cross-Assembly Libraries

The base class may come from a referenced assembly or NuGet package. The base library must be compiled with HPD's source generator so its capability and immutable settings evidence are available to the derived library.

The consumer does not need the base library’s source code. It does need compatible compiled base contracts. A library compiled before harness configuration support must be rebuilt.

## Related

- [Configure A Tool Harness](configure-a-tool-harness.md)
- [Inject Harness Dependencies](inject-harness-dependencies.md)
- [Collapsing And Containers](collapsing-and-containers.md)
