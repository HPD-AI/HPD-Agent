#:package HPD-Agent.Framework@0.9.5
#:package HPD-Agent.Providers.OpenAI@0.9.5
#:property TargetFramework=net10.0

using System.Text;
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHPDSkills();
using var serviceProvider = services.BuildServiceProvider();

var store = serviceProvider.GetRequiredService<ISkillStore>();
await store.InstallAsync(Package("1", "Validate before analysis."));

var agent = await new AgentBuilder()
    .WithServiceProvider(serviceProvider)
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<DataTools>(options => options.AddSkillsFromStore())
    .BuildAsync();

Console.WriteLine((await agent.RunAsync("Help me analyze a dataset.")).Text);

static SkillPackage Package(string version, string instructions) => new()
{
    Manifest = new SkillPackageManifest
    {
        Id = "data-analysis",
        Name = "data_analysis",
        Description = "Helps validate and analyze datasets.",
        Version = version,
        Tags = new Dictionary<string, string> { ["domain"] = "data" }
    },
    Instructions = new MemoryStream(Encoding.UTF8.GetBytes(instructions))
};

public sealed class DataTools
{
    [AIFunction(Name = "describe_active_dataset")]
    public string DescribeActiveDataset() => "The active dataset is ready.";
}
