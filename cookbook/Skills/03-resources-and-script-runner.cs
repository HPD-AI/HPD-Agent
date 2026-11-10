#:package HPD-Agent.Framework@0.9.5
#:package HPD-Agent.Providers.OpenAI@0.9.5
#:property TargetFramework=net10.0

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddSingleton<ISkillScriptRunner, DemoScriptRunner>()
    .BuildServiceProvider();

var agent = await new AgentBuilder()
    .WithServiceProvider(services)
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<DataTools>()
    .BuildAsync();

Console.WriteLine((await agent.RunAsync("Normalize the active dataset.")).Text);

public sealed class DataTools
{
    [Skill]
    public static Skill DataPreparation() => Skill.Create(
        name: "data_preparation",
        description: "Provides resources and actions for preparing datasets.",
        instructions: SkillInstructions.FromText(
            "Read the format guide, then normalize the active dataset."),
        capabilities:
        [
            SkillCapabilities.Resource(
                "read_format_guide",
                "Reads the required canonical dataset format.",
                "Use UTF-8 CSV with a unique id column."),
            SkillCapabilities.Script(
                "normalize_dataset",
                "Normalizes the active dataset and returns the resulting artifact.",
                new FileScriptReference("scripts/normalize.demo", "demo"),
                SkillScriptInput.Empty,
                requiresPermission: false)
        ]);
}

public sealed class DemoScriptRunner : ISkillScriptRunner
{
    public bool CanRun(SkillScript script) =>
        script.Reference.Runtime == "demo";

    public ValueTask<object?> RunAsync(
        SkillScriptExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Empty scripts receive the validated canonical object {}.
        Console.WriteLine($"Effective script input: {context.Arguments.Json}");
        return ValueTask.FromResult<object?>("Normalized artifact: dataset-normalized.csv");
    }
}
