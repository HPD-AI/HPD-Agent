#:package HPD-Agent.Framework@0.5.0
#:package HPD-Agent.Providers.OpenAI@0.5.0
#:property TargetFramework=net10.0

// This sample keeps conversation history by running turns in the same session and branch.

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

// Build the agent normally.
var agent = await new AgentBuilder()
                    .WithInstructions("You are a concise assistant.")
                    .WithOpenAI("gpt-5-mini")
                    .BuildAsync();

// A session is the durable conversation container. Creating it also creates
// the default branch named "main".
await agent.CreateSessionAsync("cookbook-multi-turn");

// First turn: write a user message into the session/branch history.
var first = await agent.RunAsync(
                "My name is John Doe. I am learning HPD Agent.",
                sessionId: "cookbook-multi-turn",
                branchId: "main");

Console.WriteLine(first.Text);
Console.WriteLine();

// Second turn: reuse the same session and branch. The agent loads the prior
// messages before calling the model, so it can answer from conversation memory.
var second = await agent.RunAsync(
    "What is my name, and what am I learning?",
    sessionId: "cookbook-multi-turn",
    branchId: "main");

Console.WriteLine(second.Text);
