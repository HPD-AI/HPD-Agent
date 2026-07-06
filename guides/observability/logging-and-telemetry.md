# Logging And Telemetry

Use logging when you need to understand one run while developing. Use telemetry when you need dashboards, alerts, or production trend data.

HPD Agent observes two different layers:

| Layer | What it sees | Use it for |
| --- | --- | --- |
| MEAI client layer | The `IChatClient` invocation, model metadata, request options, response timing, token usage | Provider/model latency, token usage, model-call failures |
| HPD agent layer | Message turns, iterations, tool execution, permissions, retries, middleware, compaction, checkpoints, accumulated turn usage | Explaining why the agent behaved the way it did |

The model layer tells you what happened at the provider boundary. The HPD layer tells you what happened inside the agent loop.

## Logging

Turn on logging with `WithLogging(...)`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var agent = await new AgentBuilder()
    .WithLogging(loggerFactory)
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are concise.")
    .BuildAsync();
```

`WithLogging(...)` wires three logging surfaces:

| Surface | What it logs |
| --- | --- |
| MEAI `LoggingChatClient` | Chat client invocation start, completion, cancellation, and failure. At `Trace`, it can include raw messages, options, streaming updates, and responses. |
| HPD `LoggingEventObserver` | Agent events such as message turn start/end, decisions, permission denials, retries, circuit breakers, compaction decisions, nested agent calls, and completion. |
| HPD `LoggingMiddleware` | Lifecycle hook details from message turns, iterations, functions, and tool harness expansion. |

The lifecycle middleware is registered last by `WithLogging(...)`, so it can log after earlier middleware has shaped messages, tools, and state.

## Logging Detail

Use logging options when you want less noise:

```csharp
var agent = await new AgentBuilder()
    .WithLogging(
        loggerFactory,
        LoggingMiddlewareOptions.Minimal)
    .WithOpenAI(model: "gpt-5-mini")
    .BuildAsync();
```

Built-in presets:

| Option | Behavior |
| --- | --- |
| `LoggingMiddlewareOptions.Minimal` | Function names and timing, without arguments or results. |
| `LoggingMiddlewareOptions.Default` | Message turns, iterations, functions, timing, arguments, results, and instructions with length caps. |
| `LoggingMiddlewareOptions.Verbose` | Full lifecycle logging with no string length cap. |

For custom logging:

```csharp
var agent = await new AgentBuilder()
    .WithLogging(
        loggerFactory,
        new LoggingMiddlewareOptions
        {
            LogMessageTurn = true,
            LogIteration = true,
            LogFunction = true,
            IncludeArguments = false,
            IncludeResults = false,
            MaxStringLength = 500
        })
    .WithOpenAI(model: "gpt-5-mini")
    .BuildAsync();
```

Keep `Trace` logging off in production unless your app has a deliberate data-handling policy for prompts, tool arguments, and model responses.

## Telemetry

Turn on telemetry with `WithTelemetry(...)`:

```csharp
var agent = await new AgentBuilder()
    .WithServiceProvider(services)
    .WithTelemetry(sourceName: "MyApp.Agent")
    .WithOpenAI(model: "gpt-5-mini")
    .BuildAsync();
```

`WithTelemetry(...)` wires two telemetry surfaces:

| Surface | What it emits |
| --- | --- |
| MEAI `OpenTelemetryChatClient` | GenAI semantic-convention spans and metrics for chat operations, including operation duration, time to first chunk, per-chunk timing, and per-model-call token usage. |
| HPD `TelemetryEventObserver` | Agent metrics from HPD events, including iterations, decisions, permissions, retries, compaction, checkpoints, document processing, message-turn duration, nested agent calls, and accumulated turn usage. |

Configure your host exporter separately:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("MyApp.Agent")
            .AddOtlpExporter();
    });
```

Use the same source name in `WithTelemetry(...)` and `AddMeter(...)`.

## Usage Tracking

Usage exists in two places.

MEAI telemetry reports usage for each model call. This is useful when you want provider-level token charts.

HPD accumulates usage across the whole message turn. This matters when one user request takes multiple iterations because the agent called tools, retried, requested permission, or made another model call.

Middleware can read usage while the run is still in progress:

```csharp
public sealed class TokenBudgetMiddleware : IAgentMiddleware
{
    public Task BeforeIterationAsync(
        BeforeIterationContext context,
        CancellationToken cancellationToken)
    {
        var used = context.PreviousIterationsUsage?.InputTokenCount ?? 0;

        if (used > 80_000)
        {
            context.SkipLLMCall = true;
            context.OverrideResponse = new ChatMessage(
                ChatRole.Assistant,
                "This turn has reached its token budget.");
        }

        return Task.CompletedTask;
    }
}
```

At the end of the turn:

```csharp
public Task AfterMessageTurnAsync(
    AfterMessageTurnContext context,
    CancellationToken cancellationToken)
{
    var turnUsage = context.TurnUsage;
    var perIteration = context.IterationUsage;

    return Task.CompletedTask;
}
```

The completed turn event also carries accumulated usage:

```csharp
using var sub = agent.Subscribe<MessageTurnFinishedEvent>(evt =>
{
    Console.WriteLine(evt.Usage?.TotalTokenCount);
});
```

HPD telemetry emits accumulated turn usage through these metrics when usage is available:

| Metric | Meaning |
| --- | --- |
| `agent.usage.input_tokens` | Input tokens across the completed message turn |
| `agent.usage.output_tokens` | Output tokens across the completed message turn |
| `agent.usage.total_tokens` | Total tokens across the completed message turn |
| `agent.usage.cached_input_tokens` | Cached input tokens across the completed message turn |
| `agent.usage.reasoning_tokens` | Reasoning tokens across the completed message turn |
| `agent.usage.input_audio_tokens` | Audio input tokens across the completed message turn |
| `agent.usage.input_text_tokens` | Text input tokens across the completed message turn |
| `agent.usage.output_audio_tokens` | Audio output tokens across the completed message turn |
| `agent.usage.output_text_tokens` | Text output tokens across the completed message turn |

Use MEAI usage metrics for model-call accounting. Use HPD usage state and metrics for agent-turn accounting.

## Sensitive Data

`WithTelemetry(enableSensitiveData: true)` allows the MEAI telemetry client to attach raw inputs, outputs, tool definitions, and additional provider properties when the OTel listener requests all data.

`WithLogging(...)` can also surface sensitive data depending on log level and logging options. MEAI logs raw chat data at `Trace`. HPD lifecycle logging can include instructions, tool arguments, and tool results when enabled.

Default production posture:

- keep sensitive telemetry disabled,
- avoid `Trace` logs,
- use `LoggingMiddlewareOptions.Minimal` or custom options when tool arguments/results may contain private data,
- rely on event ids, trace ids, agent names, tool names, durations, and usage metrics for dashboards.

## Related Reading

- [Events Overview](../events/overview.md)
- [Middleware Overview](../middleware/overview.md)
- [Tool And Function Events](../events/tool-and-function-events.md)
- [Lifecycle, Retry, And Error Events](../events/lifecycle-retry-and-error-events.md)
- [Live Vs Durable Events](../events/live-vs-durable-events.md)
