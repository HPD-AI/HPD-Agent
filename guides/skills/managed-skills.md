# Install And Reload Skills

Use managed skills when the host needs to install, update, list, or delete packages independently of application compilation.

Two contracts have different jobs:

```text
ISkillStore
    installs, updates, lists, and deletes packages

ISkillSource
    selects skills for one owning tool harness
```

Installing a package does not expose it globally. `AddSkillsFromStore()` explicitly attaches selected packages to a harness.

## Share One Store Across Harnesses

A store may contain packages intended for different harnesses. Add host-defined tags to package manifests and select a filtered view for each harness:

```csharp
var dataPackage = new SkillPackage
{
    Manifest = new SkillPackageManifest
    {
        Id = "data-analysis",
        Name = "data_analysis",
        Description = "Helps validate and analyze datasets.",
        Version = "1",
        Tags = new Dictionary<string, string>
        {
            ["domain"] = "data"
        }
    },
    Instructions = File.OpenRead("data-analysis.md")
};
```

```csharp
builder
    .WithToolHarness<DataTools>(options =>
        options.AddSkillsFromStore(
            SkillQuery.WithTag("domain", "data")))
    .WithToolHarness<SupportTools>(options =>
        options.AddSkillsFromStore(
            SkillQuery.WithTag("domain", "support")));
```

Use `SkillQuery.ByIds(...)` when the exact package IDs are known. The no-argument `AddSkillsFromStore()` means all packages and is appropriate when only one harness consumes that store. HPD rejects attaching the same store unfiltered to multiple harnesses because every harness would otherwise claim every package.

The store manages inventory; the attachment determines ownership. Tags do not grant permission and are not chosen by the model.

## Dependency Injection

```csharp
var services = new ServiceCollection();
services.AddHPDSkills();

using var serviceProvider = services.BuildServiceProvider();
var skillStore = serviceProvider.GetRequiredService<ISkillStore>();

await skillStore.InstallAsync(package);

var agent = await new AgentBuilder()
    .WithServiceProvider(serviceProvider)
    .WithToolHarness<DataTools>(options =>
        options.AddSkillsFromStore())
    .BuildAsync();
```

`AddHPDSkills()` preserves existing registrations. Without replacements, it provides an ephemeral `InMemoryContentStore` and a `ContentStoreSkillStore` backed by it.

For persistence, register the content store before building the service provider:

```csharp
services.AddSingleton<IContentStore>(
    new LocalFileContentStore("./agent-data"));
services.AddHPDSkills();
```

Package manifests and exact content snapshots can then be reconstructed after restart.

## Without Dependency Injection

```csharp
var contentStore = new InMemoryContentStore();
var skillStore = new ContentStoreSkillStore(
    contentStore,
    ContentStoreScopes.Skills);

await skillStore.InstallAsync(package);

var agent = await new AgentBuilder()
    .WithContentStore(contentStore)
    .WithSkillStore(skillStore)
    .WithToolHarness<DataTools>(options =>
        options.AddSkillsFromStore())
    .BuildAsync();
```

The application retains `skillStore` as its management-plane service.

## Install A Package

```csharp
await skillStore.InstallAsync(new SkillPackage
{
    Manifest = new SkillPackageManifest
    {
        Id = "data-analysis",
        Name = "data_analysis",
        Description = "Helps validate and analyze datasets.",
        Version = "1"
    },
    Instructions = File.OpenRead("instructions.md"),
    Resources =
    [
        new SkillPackageResource
        {
            Name = "read_validation_guide",
            Description = "Reads the dataset validation requirements.",
            Content = File.OpenRead("validation-guide.md"),
            ContentType = "text/markdown"
        }
    ]
});
```

Package versions are immutable. Updating requires a new version and may require the expected current version:

```csharp
await skillStore.UpdateAsync(
    "data-analysis",
    versionTwoPackage,
    expectedVersion: "1");

await skillStore.DeleteAsync(
    "data-analysis",
    expectedVersion: "2");
```

Expected versions prevent accidental overwrites. Deletion removes the current publication while allowing already-running work to finish against its retained content.

## Reload Behavior

Watchable stores notify attached agents automatically. HPD rereads and validates the complete selected inventory. Invalid changes remain installed but do not replace that agent's last valid catalog.

Manual reload is also available and detects changes made by another store or process:

```csharp
var result = await agent.ReloadSkillsAsync();

if (!result.Published)
    Console.WriteLine(result.Error);
```

Hot reload applies to instructions, resources, scripts, and runtime package metadata. Changes to compiled C# methods, generated harnesses, or assemblies require a rebuild and restart.
