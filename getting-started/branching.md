# Branching

Branches let one session split into alternate conversation paths.

Use a branch when you want to explore a different answer, draft, tool path, or subagent task without overwriting the main conversation.

## Add Program.cs

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are a concise product writing assistant.")
    .BuildAsync();

var (sessionId, mainBranchId) = await agent.CreateSessionAsync("getting-started-branches");

await agent.RunAsync(
    "We are launching a developer SDK for agent apps.",
    sessionId,
    mainBranchId);

var forkBranchId = await agent.ForkBranchAsync(
    sessionId,
    sourceBranchId: mainBranchId,
    name: "playful-draft");

var direct = await agent.RunAsync(
    "Write the launch note in a direct professional tone.",
    sessionId,
    mainBranchId);

var playful = await agent.RunAsync(
    "Write the launch note in a warmer, more playful tone.",
    sessionId,
    forkBranchId);

Console.WriteLine("Main branch:");
Console.WriteLine(direct.Text);

Console.WriteLine();
Console.WriteLine("Forked branch:");
Console.WriteLine(playful.Text);
```

Run it:

```bash
dotnet run
```

## What Happens

Both branches start from the same earlier session history.

The main branch receives the direct professional request.

The fork receives the warmer draft request.

Each branch can continue independently after the fork.

## When Branches Help

Use branches for:

- comparing alternate answers
- retrying with different instructions
- letting subagents work in isolated context
- preserving user-visible history while exploring a private path
- compacting or trimming a fork before a specialized run

## Next

Next: return to the primary path with [Save Sessions And State](persistence.md).

Go deeper: for fork options, history projection, and branch compaction, see [Branch History And Forking](../guides/sessions-and-streaming/branch-history-and-forking.md).
