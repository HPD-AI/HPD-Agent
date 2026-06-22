#:package HPD-Agent.Framework@0.5.5
#:package HPD-Agent.Providers.OpenAI@0.5.5
#:property TargetFramework=net10.0

// This sample uses local file-backed stores for sessions, agent definitions, and content.

using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

// Put every durable artifact under one local folder so the sample can be
// deleted or inspected easily after it runs.
var dataRoot = Path.Combine(Directory.GetCurrentDirectory(), ".hpd-persistence");

var sessionStore = new JsonSessionStore(Path.Combine(dataRoot, "sessions"));
var agentStore = new JsonAgentStore(Path.Combine(dataRoot, "agents"));
var contentStore = new LocalFileContentStore(Path.Combine(dataRoot, "content"));

// Seed a scoped knowledge item. The content store can later be queried by
// scope, name, tags, or other metadata.
var note = await contentStore.WriteTextAsync(
    scope: "cookbook-persistence-agent",
    text: "HPD Agent can persist sessions, stored agent definitions, and scoped content.",
    metadata: new ContentMetadata
    {
        Name = "persistence-note.txt",
        ContentType = "text/plain",
        Origin = ContentSource.System,
        Tags = new Dictionary<string, string> { ["kind"] = "knowledge" }
    });

// The agent id is the stable key for the stored definition. Persist-on-build
// writes the final serializable config into the agent store.
var agent = await new AgentBuilder()
                    .WithAgentId("cookbook-persistence-agent")
                    .WithAgentStore(agentStore, persistOnBuild: true)
                    .WithSessionStore(sessionStore, persistAfterTurn: true)
                    .WithContentStore(contentStore)
                    .WithInstructions("You are a concise assistant.")
                    .WithOpenAI("gpt-5-mini")
                    .BuildAsync();

// Re-running the sample should continue using the same persisted session
// instead of failing on duplicate session creation.
if (await sessionStore.LoadSessionAsync("cookbook-persistence") is null)
{
    await agent.CreateSessionAsync("cookbook-persistence");
}

// Because persistAfterTurn is enabled, this turn is saved after the run
// completes and can be loaded by the next process.
var result = await agent.RunAsync(
    "Remember that my release target is HPD Agent 0.5.5.",
    sessionId: "cookbook-persistence",
    threadId: "main");

Console.WriteLine(result.Text);
Console.WriteLine();

// Read the persisted indexes back directly to show what the workspace captured.
var sessionIds = await sessionStore.ListSessionIdsAsync();
var threadIds = await sessionStore.ListThreadIdsAsync("cookbook-persistence");
var agentIds = await agentStore.ListIdsAsync();
var knowledge = await contentStore.QueryAsync(
    scope: "cookbook-persistence-agent",
    query: new ContentQuery
    {
        Tags = new Dictionary<string, string> { ["kind"] = "knowledge" }
    });

Console.WriteLine($"Sessions: {string.Join(", ", sessionIds)}");
Console.WriteLine($"Threads: {string.Join(", ", threadIds)}");
Console.WriteLine($"Stored agents: {string.Join(", ", agentIds)}");
Console.WriteLine($"Knowledge items: {knowledge.Count}");
Console.WriteLine($"Latest content item: {note.Name}");
Console.WriteLine(await contentStore.ReadTextAsync("cookbook-persistence-agent", note.Id));
