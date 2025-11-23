# HPD-Agent Quick Start Guide

## Installation

```bash
dotnet add package HPD-Agent
dotnet add package HPD-Agent.Providers.OpenAI
# Add other providers as needed:
# dotnet add package HPD-Agent.Providers.Anthropic
# dotnet add package HPD-Agent.Providers.Ollama
```

---

## Basic Usage

### Simple Agent

```csharp
using HPD.Agent;

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey: "sk-...")
    .WithInstructions("You are a helpful assistant")
    .Build();

var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, "What is the capital of France?")
};

await foreach (var update in agent.RunAsync(messages))
{
    if (update is TextUpdate textUpdate)
    {
        Console.Write(textUpdate.Text);
    }
}
```

---

## Core Features

### 1. Runtime Provider Switching

Switch between providers without recreating the agent:

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", "sk-...")
    .Build();

// Use OpenAI
await agent.RunAsync(messages);

// Switch to Anthropic
agent.SwitchProvider("anthropic", "claude-3-sonnet-20240229", "sk-ant-...");

// Use Anthropic (same agent instance!)
await agent.RunAsync(messages);
```

### 2. Tools & Function Calling

```csharp
var weatherTool = AIFunctionFactory.Create(
    (string city) => $"Weather in {city}: Sunny, 72°F",
    name: "get_weather",
    description: "Gets current weather for a city");

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithTools(weatherTool)
    .Build();

var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, "What's the weather in Paris?")
};

await agent.RunAsync(messages);
// Agent will call get_weather("Paris") automatically
```

### 3. Dynamic Memory

Agent can read and write to working memory:

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithDynamicMemory(config =>
    {
        config.Enabled = true;
        config.Instructions = "Store important facts about the user";
    })
    .Build();

// Agent can now remember facts across conversations
```

### 4. Custom Middleware

Add custom processing layers:

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)

    // Rate limiting
    .UseChatClientMiddleware((client, _) =>
        new RateLimitingChatClient(client, maxRequestsPerMinute: 60))

    // Cost tracking
    .UseChatClientMiddleware((client, services) =>
        new CostTrackingChatClient(client, services.GetService<ICostTracker>()))

    .Build();
```

### 5. Options Configuration

Transform options before each request:

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithOptionsConfiguration(opts =>
    {
        // Cap temperature
        opts.Temperature = Math.Min(opts.Temperature ?? 1.0f, 0.8f);

        // Add tracking
        opts.AdditionalProperties ??= new();
        opts.AdditionalProperties["request_id"] = Guid.NewGuid().ToString();
    })
    .Build();
```

---

## Common Scenarios

### Multi-Provider Fallback

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", "sk-...")
    .Build();

var providers = new[]
{
    ("openai", "gpt-4"),
    ("anthropic", "claude-3-sonnet-20240229"),
    ("ollama", "llama3.2")
};

foreach (var (provider, model) in providers)
{
    try
    {
        if (provider != agent.Config.Provider.ProviderKey)
            agent.SwitchProvider(provider, model, GetApiKey(provider));

        return await agent.RunAsync(messages);
    }
    catch (Exception ex) when (IsTransientError(ex))
    {
        Console.WriteLine($"Failed with {provider}, trying next...");
    }
}
```

### Streaming Responses

```csharp
await foreach (var update in agent.RunAsync(messages))
{
    switch (update)
    {
        case TextUpdate text:
            Console.Write(text.Text);
            break;

        case ToolCallUpdate toolCall:
            Console.WriteLine($"\nCalling tool: {toolCall.Name}");
            break;

        case CompletionUpdate completion:
            Console.WriteLine($"\nTokens used: {completion.TotalTokens}");
            break;
    }
}
```

### With Filters

```csharp
public class LoggingFilter : IPromptMiddleware
{
    public async Task<IEnumerable<ChatMessage>> FilterAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next)
    {
        Console.WriteLine($"Sending {context.Messages.Count()} messages");
        return await next(context);
    }
}

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithPromptMiddleware(new LoggingFilter())
    .Build();
```

### With Conversation History

```csharp
var history = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, "My name is Alice"),
    new ChatMessage(ChatRole.Assistant, "Hello Alice, nice to meet you!"),
    new ChatMessage(ChatRole.User, "What's my name?")
};

await agent.RunAsync(history);
// Output: "Your name is Alice"
```

---

## Configuration Options

### Provider Configuration

```csharp
.WithProvider(
    providerKey: "openai",           // Provider identifier
    modelName: "gpt-4",              // Model name
    apiKey: "sk-...",                // API key
    endpoint: null,                  // Optional custom endpoint
    defaultOptions: new ChatOptions  // Optional default options
    {
        Temperature = 0.7f,
        MaxOutputTokens = 2000
    })
```

### Agentic Loop Configuration

```csharp
.WithAgenticLoop(config =>
{
    config.MaxIterations = 10;           // Max tool call iterations
    config.TerminateOnUnknownCalls = true; // Stop on unknown tool calls
})
```

### Error Handling

```csharp
.WithErrorHandling(config =>
{
    config.MaxRetries = 3;
    config.NormalizeErrors = true;       // Normalize provider errors
    config.IncludeProviderDetails = false; // Hide provider details
})
```

### Telemetry

```csharp
.WithTelemetry(config =>
{
    config.Enabled = true;
    config.ActivitySourceName = "MyApp.Agent";
})
```

### Logging

```csharp
.WithLogging(config =>
{
    config.Enabled = true;
    config.LogLevel = LogLevel.Information;
})
```

---

## Advanced Usage

### Custom Provider

```csharp
// 1. Implement IProviderFeatures
public class CustomProvider : IProviderFeatures
{
    public string ProviderKey => "custom";
    public string DisplayName => "Custom Provider";

    public IChatClient CreateChatClient(ProviderConfig config, IServiceProvider? services)
    {
        return new CustomChatClient(config);
    }

    public IProviderErrorHandler CreateErrorHandler()
    {
        return new CustomErrorHandler();
    }

    // ... other methods
}

// 2. Register via ModuleInitializer
[ModuleInitializer]
public static void Initialize()
{
    ProviderDiscovery.RegisterProviderFactory(() => new CustomProvider());
}

// 3. Use it
var agent = new AgentBuilder()
    .WithProvider("custom", "my-model", apiKey)
    .Build();
```

### Skills (Auto-discovered Tools)

```csharp
[HPDSkill(
    Name = "Calculator",
    Description = "Mathematical calculations")]
public class CalculatorSkill
{
    [HPDFunction(Description = "Adds two numbers")]
    public int Add(int a, int b) => a + b;

    [HPDFunction(Description = "Multiplies two numbers")]
    public int Multiply(int a, int b) => a * b;
}

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithSkill<CalculatorSkill>()
    .Build();
```

### Permissions & Security

```csharp
public class PermissionMiddleware : IPermissionMiddleware
{
    public Task<PermissionResult> CheckPermissionAsync(
        PermissionContext context,
        CancellationToken cancellationToken)
    {
        // Block file system access
        if (context.ToolName.Contains("file"))
        {
            return Task.FromResult(PermissionResult.Deny("File access not allowed"));
        }

        return Task.FromResult(PermissionResult.Allow());
    }
}

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithPermissionMiddleware(new PermissionMiddleware())
    .Build();
```

---

## Best Practices

### 1. Always Handle Errors

```csharp
try
{
    await agent.RunAsync(messages);
}
catch (RateLimitException ex)
{
    // Handle rate limits
    agent.SwitchProvider("anthropic", "claude-3-haiku-20240307", apiKey);
    await agent.RunAsync(messages);
}
catch (Exception ex)
{
    // Log and handle other errors
    logger.LogError(ex, "Agent failed");
}
```

### 2. Use Cancellation Tokens

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await agent.RunAsync(messages, cancellationToken: cts.Token);
```

### 3. Dispose Resources

```csharp
// Agents don't need disposal, but if using IServiceProvider:
await using var services = new ServiceCollection()
    .AddLogging()
    .BuildServiceProvider();

var agent = new AgentBuilder()
    .WithServiceProvider(services)
    .Build();
```

### 4. Configure Timeouts

```csharp
.WithOptionsConfiguration(opts =>
{
    opts.AdditionalProperties ??= new();
    opts.AdditionalProperties["timeout"] = TimeSpan.FromSeconds(30);
})
```

### 5. Monitor Costs

```csharp
var costTracker = new CostTracker();

var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .UseChatClientMiddleware((client, _) =>
        new CostTrackingChatClient(client, costTracker))
    .Build();

await agent.RunAsync(messages);

Console.WriteLine($"Total cost: ${costTracker.TotalCost:F2}");
```

---

## Troubleshooting

### Provider Not Found

```
InvalidOperationException: Provider 'anthropic' not found.
```

**Solution:** Install the provider package:
```bash
dotnet add package HPD-Agent.Providers.Anthropic
```

### Rate Limit Errors

```
RateLimitException: Rate limit exceeded
```

**Solution:** Add rate limiting middleware or switch providers:
```csharp
.UseChatClientMiddleware((client, _) =>
    new RateLimitingChatClient(client, maxRequestsPerMinute: 60))
```

### Middleware Returns Null

```
InvalidOperationException: Chat client middleware returned null
```

**Solution:** Ensure middleware always returns a valid client:
```csharp
.UseChatClientMiddleware((client, services) =>
{
    if (client == null) throw new ArgumentNullException(nameof(client));
    return new CustomChatClient(client);
})
```

---

## Examples Repository

Find complete examples at:
- [Basic Chat](../Examples/BasicChat/)
- [Multi-Provider Fallback](../Examples/ProviderFallback/)
- [Tool Calling](../Examples/ToolCalling/)
- [Custom Middleware](../Examples/CustomMiddleware/)
- [Skills Demo](../Examples/Skills/)

---

## Next Steps

- Read [RUNTIME_PROVIDER_SWITCHING.md](RUNTIME_PROVIDER_SWITCHING.md) for advanced provider switching
- Read [ARCHITECTURE_OVERVIEW.md](ARCHITECTURE_OVERVIEW.md) for architecture details
- Explore the [Examples](../Examples/) folder
- Check out [API Reference](API_REFERENCE.md)

---

## Getting Help

- **Documentation**: [/Docs](.)
- **Issues**: [GitHub Issues](https://github.com/your-org/HPD-Agent/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/HPD-Agent/discussions)
