#:project ../../../HPD-OS/HPD-AI-Framework/dotnet/HPD-Agent.Framework/src/HPD-Agent/HPD-Agent.csproj
#:project ../../../HPD-OS/HPD-AI-Framework/dotnet/HPD-Agent.Framework/src/HPD-Agent.Providers/HPD-Agent.Providers.OpenAI/HPD-Agent.Providers.OpenAI.csproj
#:property TargetFramework=net10.0

// This sample forks one conversation into two branches with shared history.

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

// Build the agent normally.
var agent = await new AgentBuilder()
                    .WithInstructions("You are a concise naming assistant.")
                    .WithOpenAI("gpt-5-mini")
                    .BuildAsync();

// A session can contain multiple branches. The first branch is always "main".
await agent.CreateSessionAsync("cookbook-branching");

// Start on main. The fork below will copy this shared setup history.
var setup = await agent.RunAsync(
    "I am naming a coffee shop. The name should feel calm, modern, and friendly.",
    sessionId: "cookbook-branching",
    branchId: "main");

Console.WriteLine("Shared setup:");
Console.WriteLine(setup.Text);
Console.WriteLine();

// Fork main from its latest message into a new branch named "playful".
// From here on, each branch can evolve independently.
await agent.ForkBranchAsync(
    sessionId: "cookbook-branching",
    sourceBranchId: "main",
    newBranchId: "playful");

// Continue the original branch with the neutral naming direction.
var mainResult = await agent.RunAsync(
    "Suggest one name.",
    sessionId: "cookbook-branching",
    branchId: "main");

// Continue the forked branch from the same shared setup, but ask for a
// different tone. This is useful for comparing alternatives without losing history.
var playfulResult = await agent.RunAsync(
    "Suggest one name with a more playful tone.",
    sessionId: "cookbook-branching",
    branchId: "playful");

Console.WriteLine("Main branch:");
Console.WriteLine(mainResult.Text);
Console.WriteLine();
Console.WriteLine("Playful branch:");
Console.WriteLine(playfulResult.Text);
