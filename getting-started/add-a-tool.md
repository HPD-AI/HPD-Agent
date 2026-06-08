# Add A Tool

Tools let the model call C# methods. This page adds one local weather function.

Continue in the same `HpdAgentQuickstart` folder from [Hello Agent](hello-agent.md). This is the same shape as the first agent, with one added tool class and one `.WithTool(...)` call.

## Add Program.cs

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;
using Microsoft.Extensions.AI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("Use the weather tool when the user asks about weather.")
    .WithTool<WeatherToolHarness>("get_weather")
    .BuildAsync();

var result = await agent.RunAsync("What is the weather in Chicago?");
Console.WriteLine(result.Text);

public class WeatherToolHarness
{
    [AIFunction(Name = "get_weather")]
    [AIDescription("Gets the current weather for a location.")]
    public string GetWeather(
        [AIDescription("The city or location to get weather for.")] string location)
        => $"It is sunny and 72 F in {location}.";
}
```

Run it:

```bash
dotnet run
```

## You Succeeded If

The answer should mention the fake weather returned by `GetWeather`, for example:

```text
The weather in Chicago is sunny and 72 F.
```

## What Happens

`WeatherToolHarness` is an ordinary C# class that contains a method the model can call.

`[AIFunction(Name = "get_weather")]` exposes the method as a function named `get_weather`.

`[AIDescription(...)]` describes the function and parameter so the model has enough context to decide when and how to call it.

`.WithTool<WeatherToolHarness>("get_weather")` registers one function from the harness for this agent.

`RunAsync(...)` sends the user request. Because the instructions tell the agent to use the weather tool for weather questions, the model can call `get_weather` and use the returned string in its answer.

`result.Text` contains the final assistant answer after any tool call has completed.

## Next

Next: keep conversation history in [Multi-Turn Sessions](multi-turn-sessions.md).

Optional: group related functions in [Tool Harnesses](tool-harness.md).

Go deeper: selective registration, tool configuration, source generation behavior, collapsing, MCP, OpenAPI, and built-in harnesses belong in [Tools, Functions, And Harnesses](../concepts/tools-functions-and-harnesses.md).

Tools often interact with events and middleware. Tool calls can appear in the event stream, and middleware can add behavior around function execution. See [Events](../reference/events.md) and [Middleware Lifecycle](../concepts/middleware-lifecycle.md).

## Troubleshooting

If a tool harness is not found in the registry, the harness may not have been generated or discovered, or it may not contain source-generator-recognized capability attributes.

If a function is not found on the tool harness, check that the function name passed to `.WithTool<T>(...)` matches the generated or discovered function name. In this example, both use `get_weather`.

If the model answers without using the tool, make the instruction and function description more direct. The model chooses tools based on the user request, instructions, function name, and descriptions.
