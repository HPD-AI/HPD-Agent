#:package HPD-Agent.Framework@0.6.0
#:property TargetFramework=net10.0

using System.Text;
using HPD.Agent;

var content = new InMemoryContentStore();
var store = new ContentStoreSkillStore(content, ContentStoreScopes.Skills);
await store.InstallAsync(Package("1", "Version one instructions."));

var agent = await new AgentBuilder(new AgentConfig { Name = "managed-skills" })
    .WithContentStore(content)
    .WithSkillStore(store)
    .WithToolHarness<DataTools>(options => options.AddSkillsFromStore())
    .BuildAsync();

await store.UpdateAsync(
    "data-analysis",
    Package("2", "Version two instructions."),
    expectedVersion: "1");

var reload = await agent.ReloadSkillsAsync();
Console.WriteLine($"Published: {reload.Published}; epoch: {reload.Epoch}");

await store.DeleteAsync("data-analysis", expectedVersion: "2");

static SkillPackage Package(string version, string instructions) => new()
{
    Manifest = new SkillPackageManifest
    {
        Id = "data-analysis",
        Name = "data_analysis",
        Description = "Helps validate and analyze datasets.",
        Version = version
    },
    Instructions = new MemoryStream(Encoding.UTF8.GetBytes(instructions))
};

public sealed class DataTools
{
    [AIFunction(Name = "describe_active_dataset")]
    public string DescribeActiveDataset() => "The active dataset is ready.";
}
