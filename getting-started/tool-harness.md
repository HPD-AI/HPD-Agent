# Tool Harnesses

A tool harness groups related C# capabilities and registers them with one builder call.

Use a harness when the agent should see a small tool surface that belongs together, such as support tools, file tools, account tools, or project tools.

## Add Program.cs

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;
using Microsoft.Extensions.AI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("Use the support tools when they help answer the user.")
    .WithToolHarness<SupportToolHarness>()
    .BuildAsync();

var result = await agent.RunAsync("Look up ticket 4815 and summarize the next action.");
Console.WriteLine(result.Text);

public class SupportToolHarness
{
    [AIFunction(Name = "lookup_ticket")]
    [AIDescription("Looks up a support ticket by id.")]
    public string LookupTicket(
        [AIDescription("The support ticket id.")] string ticketId)
        => $"Ticket {ticketId}: customer cannot sign in after changing email.";

    [AIFunction(Name = "suggest_next_action")]
    [AIDescription("Suggests the next support action for a ticket summary.")]
    public string SuggestNextAction(
        [AIDescription("The ticket summary.")] string summary)
        => "Verify the new email address, reset active sessions, and ask the customer to sign in again.";
}
```

Run it:

```bash
dotnet run
```

## What Happens

`SupportToolHarness` is the unit of registration.

`WithToolHarness<SupportToolHarness>()` exposes all generated functions from that harness.

Each `[AIFunction]` method becomes a tool the model can call. The method name, descriptions, and parameter descriptions shape how the model chooses and fills the tool call.

Use `WithTool<T>("function_name")` when the agent should see one function. Use `WithToolHarness<T>()` when the agent should see the whole harness.

## Keep The First Harness Small

Start with two or three functions that clearly belong together. A smaller tool surface usually gives the model a cleaner choice.

When the harness grows, split it by workflow or audience. For example, expose `SupportToolHarness` to support agents and `BillingToolHarness` to billing agents instead of one large general harness.

## Next

Next: return to the primary path with [Multi-Turn Sessions](multi-turn-sessions.md).

Go deeper: for selective registration, configured harnesses, collapsed tool containers, permissions, subagents, and source generation behavior, see [Author A Tool Harness](../guides/tools/author-a-tool-harness.md) and [Tools, Functions, And Harnesses](../concepts/tools-functions-and-harnesses.md).
