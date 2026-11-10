#:sdk Microsoft.NET.Sdk.Web
#:package HPD-Agent.AspNetCore@0.9.5
#:package HPD-Agent.Providers.OpenAI@0.9.5
#:property TargetFramework=net10.0

// This sample hosts an HPD Agent as an ASP.NET Core API.

using HPD.Agent;
using HPD.Agent.AspNetCore;
using HPD.Agent.Providers.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Register one named hosted agent. The name selects this hosting configuration
// when routes call MapHPDAgentHostApi("cookbook-agent").
builder.Services.AddHPDAgentHost("cookbook-agent", options =>
{
    // Keep server state next to the sample so sessions and stored agent
    // definitions survive app restarts but remain easy to delete.
    var dataRoot = Path.Combine(Directory.GetCurrentDirectory(), ".hpd-hosting");

    // The hosting layer owns persistence. It passes the same session store to
    // agent runtimes when streaming requests arrive.
    options.SessionStorePath = Path.Combine(dataRoot, "sessions");
    options.AgentStore = new JsonAgentStore(Path.Combine(dataRoot, "agents"));
    options.DefaultAgentId = "cookbook-agent";

    // DefaultAgent declares the definition hosting creates for this profile at startup.
    // Runtime requests resolve stored definitions only.
    options.DefaultAgent = new AgentConfig
    {
        Name = "Cookbook Agent",
        SystemInstructions = "You are a hosted HPD Agent. Be concise and helpful.",
    };
    options.ConfigureEachAgentBuilder = agent => agent.WithOpenAI("gpt-5-mini");
});

var app = builder.Build();

app.MapGet("/", () => "HPD Agent is hosted. Try POST /hpd/sessions or the streaming endpoints.");

var hpd = app.MapGroup("/hpd");
hpd.MapHPDAgentRuntimeApi("cookbook-agent");
hpd.MapHPDAgentAdministrationApi("cookbook-agent");

app.Run();
