# Create Skills In C#

Declare a skill as a `[Skill]` method in its owning tool harness:

```csharp
using HPD.Agent;

public sealed partial class DataTools
{
    [AIFunction(Name = "validate_dataset")]
    [AIDescription("Validates the active dataset and reports problems.")]
    public string ValidateDataset()
    {
        return "Dataset is valid.";
    }

    [Skill]
    public static Skill DataAnalysis() => Skill.Create(
        name: "data_analysis",
        description: "Helps validate and analyze datasets.",
        instructions: SkillInstructions.FromText("""
            Read the validation guide first.
            Validate the dataset before analyzing it.
            Explain any validation failures.
            """),
        capabilities:
        [
            SkillCapabilities.Function<DataTools>(nameof(ValidateDataset)),
            SkillCapabilities.Resource(
                name: "read_validation_guide",
                description: "Reads the dataset validation requirements.",
                content: "Every row must have a unique identifier.")
        ]);
}
```

Register the harness normally:

```csharp
var agent = await new AgentBuilder()
    .WithToolHarness<DataTools>()
    .BuildAsync();
```

## Authoring Rules

`name` is the model-visible activation function name. `description` is visible before activation, so keep it short and focused on when to select the skill. `instructions` are returned only after activation.

Use `SkillCapabilities.Function<T>(nameof(Member))` for C# function references. The referenced function may belong to another generated harness, but that harness must be available to the agent. HPD validates missing functions and conflicting model-facing names during generation or agent construction.

Every model-selectable resource and script requires a useful description. Describe what it returns or changes and when it should be selected; do not merely repeat its filename.

Skill and child capability names must contain 1-64 ASCII letters, digits,
underscores, or hyphens and must start with a letter or underscore. Generated
declarations report `HPDSKILL019` for invalid names; runtime and managed-package
validation reject the same names during construction or installation.

Skill activation lasts for the remainder of the current message turn. No other
activation lifetime is exposed in 1.0.

## Optional Reinforcement

Activation instructions are always returned in the tool result. For critical rules that benefit from higher-priority emphasis, add reinforcement:

```csharp
reinforcement: SkillInstructions.FromText(
    "Never analyze a dataset that has not passed validation.")
```

Enable delivery through configuration:

```csharp
var config = new AgentConfig
{
    Skills = new SkillRuntimeOptions
    {
        InstructionDelivery =
            SkillInstructionDelivery.ToolResultWithSystemReinforcement
    }
};
```

Reinforcement supplements the activation result; it does not replace it.

Generated `[Skill]` declarations require source generation. They do not use the JIT reflection fallback available to some ordinary tool functions.
