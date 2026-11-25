# Lifecycle, Retry, And Error Events

Lifecycle, retry, and error events help UIs and logs explain how a run moved through message turns, model calls, function calls, retries, and failures.

Use them for timelines, diagnostics, and operational visibility. Do not render every diagnostic event as user-facing output.

## Message Turn Lifecycle

A message turn is the user-facing unit of work for one run input.

```csharp
using var started = agent.Subscribe<MessageTurnStartedEvent>(evt =>
{
    timeline.StartTurn(evt.TraceId, evt.AgentName);
});

using var finished = agent.Subscribe<MessageTurnFinishedEvent>(evt =>
{
    timeline.FinishTurn(evt.TraceId, evt.Duration);
});

using var failed = agent.Subscribe<MessageTurnErrorEvent>(evt =>
{
    if (evt.ClientSetup is { } setup)
    {
        timeline.FailClientSetup(
            evt.TraceId,
            setup.Code,
            setup.Message,
            setup.Guidance);
    }
    else
    {
        timeline.FailTurn(evt.TraceId, evt.ErrorMessage);
    }
});
```

Use message-turn events for whole-run activity. Use text, reasoning, and tool events for the details inside the turn.

`ClientSetup` is present when provider or client resolution prevents the run from reaching a model call. Its fields include the stable failure code, safe guidance, client family, and any known provider, backend, model, configuration path, or provider validation issues. A direct `RunAsync(...)` caller still receives the original typed exception; the event does not replace host exception handling.

## Agent Turn Lifecycle

An agent turn is one model-call iteration inside a message turn. A single user message can involve multiple agent turns when tools are called.

```text
message turn
  agent turn 1
    model streams tool call
    tool executes
  agent turn 2
    model streams final answer
```

Use `AgentTurnStartedEvent` and `AgentTurnFinishedEvent` for iteration-level timelines and debugging.

## Retry Events

`FunctionRetryEvent` reports a function retry. `ModelCallRetryEvent` reports a model/provider retry.

```csharp
using var modelRetries = agent.Subscribe<ModelCallRetryEvent>(evt =>
{
    timeline.MarkModelRetry(evt.Attempt, evt.MaxRetries, evt.Delay);
});

using var functionRetries = agent.Subscribe<FunctionRetryEvent>(evt =>
{
    timeline.MarkFunctionRetry(evt.FunctionName, evt.Attempt, evt.ExceptionType);
});
```

Streaming UIs should treat model retry events carefully. A model retry can arrive after partial assistant text has already been displayed. Mark or clear stale partial output before rendering retry output.

Function retries are usually easier to render because tool results are not token-streamed.

## Middleware And Diagnostic Errors

Middleware and framework diagnostics can emit events such as:

- `MiddlewareErrorEvent`
- `CircuitBreakerTriggeredEvent`
- `MaxConsecutiveErrorsExceededEvent`
- `TotalErrorThresholdExceededEvent`
- retry events
- structured output error events

Render these in a debug or observability lane unless the product intentionally exposes them to users.

## Application Error Handling

Events are observability. They do not replace normal `try`/`catch` around application calls:

```csharp
try
{
    await agent.RunAsync("Use the tools if needed.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Agent run failed.");
}
```

Use events for live rendering and diagnostics. Use application exception handling for call-site control flow.

## Related Pages

- [Error Handling Middleware](../middleware/error-handling.md)
- [Render An Event Stream](../sessions-and-streaming/render-an-event-stream.md)
- [Events Reference](../../reference/events.md)
