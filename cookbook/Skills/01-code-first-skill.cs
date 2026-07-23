#:package HPD-Agent.Framework@0.6.0
#:package HPD-Agent.Providers.OpenAI@0.6.0
#:property TargetFramework=net10.0

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<DataTools>()
    .BuildAsync();

Console.WriteLine((await agent.RunAsync("Validate the active dataset.")).Text);

public sealed class DataTools
{
    [AIFunction(Name = "validate_dataset")]
    [AIDescription("Validates the active dataset and reports problems.")]
    public string ValidateDataset() => "Dataset is valid.";

    [Skill]
    public static Skill DataAnalysis() => Skill.Create(
        name: "data_analysis",
        description: "Helps validate and analyze datasets.",
        instructions: SkillInstructions.FromText(
            "Read the guide and validate the dataset before analysis."),
        capabilities:
        [
            SkillCapabilities.Function<DataTools>(nameof(ValidateDataset)),
            SkillCapabilities.Resource(
                "read_validation_guide",
                "Reads the dataset validation requirements.",
                "Every row must have a unique identifier.")
        ]);
}
