# Quick Start Guide

# HPD.Agent Quick Start

**Get a production-ready AI agent running in under 5 minutes.**

HPD.Agent handles the following automatically:
- Conversation history tracking
- Real-time streaming responses
- Multi-turn context management
- Function calling / tool use
- Provider abstraction (OpenAI, Anthropic, Ollama, etc.)

## Your First Agent

### 1. Install the package

```bash
dotnet add package HPD.Agent
dotnet add package HPD.Agent.Providers.OpenAI
```

### 2. Write 10 lines of code

Create a new file `Program.cs`:

```csharp
using HPD.Agent;

var agent = new AgentBuilder()
    .WithInstructions("You are a helpful assistant")
    .WithProvider("openai", "gpt-4o", apiKey: "sk-...")
    .Build();

var thread = agent.CreateThread();

await foreach (var evt in agent.RunAsync("What's the weather in Tokyo?", thread))
{
    if (evt is TextDeltaEvent delta)
        Console.Write(delta.Text);
}
```

### 3. Run it

```bash
dotnet run
```

**Expected output:**
```
I don't have access to real-time weather data, but I can help you find...
```

You now have a working AI agent.

---

## What Just Happened?

That 10-line example did the following automatically:

```csharp
var agent = new AgentBuilder()
    .WithInstructions("You are a helpful assistant")  // Sets personality
    .WithProvider("openai", "gpt-4o", apiKey: "sk-...")  // Connects to LLM
    .Build();
```
- Agent configured with OpenAI
- Streaming enabled by default
- Error handling included

```csharp
var thread = agent.CreateThread();
```
- In-memory conversation thread created
- History tracking starts automatically
- Ready for multi-turn conversations

```csharp
await foreach (var evt in agent.RunAsync("...", thread))
```
- Message sent to LLM
- Response streamed in real-time
- Conversation history updated automatically

---

## Making it Better

### Don't hardcode your API key

Create `appsettings.json`:

```json
{
  "Providers": {
    "OpenAI": {
      "ProviderKey": "openai",
      "ModelName": "gpt-4o",
      "ApiKey": "sk-..."
    }
  }
}
```

Now your code gets even simpler:

```csharp
var agent = new AgentBuilder()
    .WithInstructions("You are a helpful assistant")
    .Build();  // Automatically loads from appsettings.json
```

You can also use environment variables or user secrets:

```bash
# Set environment variable
export OPENAI_API_KEY="sk-..."

# Or use dotnet user secrets (recommended for development)
dotnet user-secrets init
dotnet user-secrets set "Providers:OpenAI:ApiKey" "sk-..."
```

Now you can commit your code without leaking keys:

```csharp
var agent = new AgentBuilder()
    .WithInstructions("You are a helpful assistant")
    .Build();  // Loads from environment or user secrets
```

---

## Multi-Turn Conversations

Continue the conversation with multi-turn support:

```csharp
var agent = new AgentBuilder()
    .WithInstructions("You are a helpful assistant")
    .Build();

var thread = agent.CreateThread();
thread.DisplayName = "My First Chat";  // Optional: friendly name for UI

// First message
Console.WriteLine("You: What's 2+2?");
await foreach (var evt in agent.RunAsync("What's 2+2?", thread))
{
    if (evt is TextDeltaEvent delta)
        Console.Write(delta.Text);
}
Console.WriteLine("\n");

// Second message - agent remembers context
Console.WriteLine("You: What about 2+3?");
await foreach (var evt in agent.RunAsync("What about 2+3?", thread))
{
    if (evt is TextDeltaEvent delta)
        Console.Write(delta.Text);
}
```

**Output:**
```
You: What's 2+2?
2+2 equals 4.

You: What about 2+3?
2+3 equals 5.
```

The agent remembers the conversation; no manual history management needed.

---

## Adding Tools (Function Calling)

Add capabilities to your agent:

### 1. Define a plugin

```csharp
public class WeatherPlugin
{
    [AIFunction]
    [AIDescription("Get the current weather for a city")]
    public string GetWeather([AIDescription("City name")] string city)
    {
        // In a real app, call a weather API
        return $"The weather in {city} is sunny, 72°F";
    }
}
```

### 2. Register it with the agent

```csharp
var agent = new AgentBuilder()
    .WithInstructions("You are a helpful weather assistant")
    .WithPlugin<WeatherPlugin>()
    .Build();

var thread = agent.CreateThread();

await foreach (var evt in agent.RunAsync("What's the weather in Tokyo?", thread))
{
    if (evt is TextDeltaEvent delta)
        Console.Write(delta.Text);
}
```

**Output:**
```
The weather in Tokyo is sunny, 72°F. It's a beautiful day!
```

The agent:
- Detects it needs weather data
- Calls `GetWeather("Tokyo")`
- Uses the result to answer naturally

---

## Handling Events

The event stream allows for custom UI handling:

```csharp
await foreach (var evt in agent.RunAsync("Hello!", thread))
{
    switch (evt)
    {
        case TextMessageStartEvent start:
            Console.Write("Agent: ");
            break;
        case TextDeltaEvent delta:
            Console.Write(delta.Text);
            break;
        case TextMessageEndEvent end:
            Console.WriteLine("\n");
            break;
        case ToolCallStartEvent toolStart:
            Console.WriteLine($"Using tool: {toolStart.Name}");
            break;
        case ToolCallResultEvent toolResult:
            Console.WriteLine($"Tool result: {toolResult.Result}");
            break;
    }
}
```

**Output:**
```
Using tool: GetWeather
Tool result: The weather in Tokyo is sunny, 72°F
Agent: The weather in Tokyo is sunny, 72°F. It's a beautiful day!
```

---

## Common Patterns

### Pattern: Interactive Chat Loop

```csharp
var agent = new AgentBuilder().Build();
var thread = agent.CreateThread();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    Console.Write("Agent: ");
    await foreach (var evt in agent.RunAsync(input, thread))
    {
        if (evt is TextDeltaEvent delta)
            Console.Write(delta.Text);
    }
    Console.WriteLine("\n");
}
```

### Pattern: Extract Final Response

```csharp
var responseText = new StringBuilder();

await foreach (var evt in agent.RunAsync("Hello", thread))
{
    if (evt is TextDeltaEvent delta)
        responseText.Append(delta.Text);
}

Console.WriteLine($"Full response: {responseText}");
```

### Pattern: Switch Providers

```csharp
// Use OpenAI for development
var devAgent = new AgentBuilder()
    .WithProvider("openai", "gpt-4o-mini", apiKey: openaiKey)
    .Build();

// Use Anthropic for production
var prodAgent = new AgentBuilder()
    .WithProvider("anthropic", "claude-sonnet-4", apiKey: anthropicKey)
    .Build();

// Same code works with both
```

---

## Next Steps

You now have a working agent. See the following guides for more:

- [Building Plugins](./plugins.md): Add tools, memory, and skills
- [Persistence Guide](./persistence.md): Save to database, resume from crashes
- [Production Checklist](./production.md): Logging, monitoring, scaling
- [Core Concepts](./concepts.md): Deep dive into the architecture

---

## Troubleshooting

### "No provider registered"
Make sure you installed the provider package:
```bash
dotnet add package HPD.Agent.Providers.OpenAI
# or
dotnet add package HPD.Agent.Providers.Anthropic
# or
dotnet add package HPD.Agent.Providers.Ollama
```

### "API key not found"
The framework looks for API keys in this order:
1. `appsettings.json` → `Providers:OpenAI:ApiKey`
2. Environment variable → `OPENAI_API_KEY`
3. User secrets → `dotnet user-secrets set "Providers:OpenAI:ApiKey" "sk-..."`
4. Direct in code → `.WithProvider("openai", "gpt-4o", apiKey: "sk-...")`

### Events not appearing
Make sure you're checking the right event types:
```csharp
await foreach (var evt in agent.RunAsync("Hello", thread))
{
    Console.WriteLine($"Event type: {evt.GetType().Name}");
    // See what events are actually being emitted
}
```

---

## Summary

- Create an agent in 10 lines of code
- Configure providers (OpenAI, Anthropic, etc.)
- Handle multi-turn conversations automatically
- Add tools via plugins
- Process streaming events
- Build interactive chat interfaces

HPD.Agent handles state management automatically. You just call `RunAsync()` and everything else (history tracking, context management, checkpointing) happens behind the scenes.

---

For more advanced usage, see [Building Plugins](./plugins.md).

---

## Key Features of This Quick Start

1. Immediate working code in step 1
2. Progressive complexity: starts simple, builds up gradually
3. Answers the "where's my API key?" question early
4. Demonstrates multi-turn conversations
5. Realistic code patterns
6. Clear next steps for further learning
7. Troubleshooting section for common issues

## What Is Not Covered Here

- No mention of checkpointing
- No middleware explanation
- No discussion of message stores
- No AOT compilation details
- No explanation of the event architecture

These topics are covered in later sections as needed.

