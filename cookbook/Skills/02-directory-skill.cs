#:package HPD-Agent.Framework@0.6.0
#:package HPD-Agent.Providers.OpenAI@0.6.0
#:property TargetFramework=net10.0

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

// Run this sample from the repository root.
var skillDirectory = Path.GetFullPath(
    "cookbook/Skills/assets/data-analysis");

var agent = await new AgentBuilder()
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<DataTools>(options =>
        options.AddSkillSource(new DirectorySkillSource(
            skillDirectory,
            SkillDirectoryImportMode.Strict)))
    .BuildAsync();

Console.WriteLine((await agent.RunAsync("Show me how to validate a dataset.")).Text);

public sealed class DataTools
{
    [AIFunction(Name = "describe_active_dataset")]
    public string DescribeActiveDataset() => "The active dataset is ready.";
}
