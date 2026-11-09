# Error Handling Middleware

Error handling is split across wrapper middleware, iteration safety middleware, events, and normal application exception handling.

For UI rendering and event capture, see [Lifecycle, Retry, And Error Events](../events/lifecycle-retry-and-error-events.md).

`OnErrorAsync` is the terminal failure observer for middleware execution. It runs
exactly once for model, function, iteration, message-turn, input, runtime, and
thread-fork failures. Caller cancellation stays on the cancellation lifecycle and
does not invoke `OnErrorAsync`.

## Choose The Boundary

Use the boundary that matches the failure:

| Failure | Primary mechanism | Notes |
| --- | --- | --- |
| Transient model/provider streaming error | `RetryMiddleware` model wrapper, then `ErrorFormattingMiddleware` model wrapper | On final failure, `OnErrorAsync` receives `ErrorSource.ModelCall`; `ModelCallRetryEvent` can arrive after partial text streamed. |
| Function body exception | Function wrappers, `MiddlewareErrorEvent`, `OnErrorAsync`, then `AfterFunctionAsync` formatting | `OnErrorAsync` receives `ErrorSource.ToolCall` before result formatting. |
| Slow function | `FunctionTimeoutMiddleware` | Put timeout inside retry for per-attempt timeout. |
| Repeated tool errors or loops | `ErrorTrackingMiddleware`, `TotalErrorThresholdIterationMiddleware`, `CircuitBreakerMiddleware` | These are iteration safety policies, not exception formatters. |
| Whole turn failure | `OnErrorAsync`, `MessageTurnErrorEvent`, and the exception from `RunAsync(...)` | `OnErrorAsync` receives `ErrorSource.MessageTurn`; the original failure still propagates. |

## Recommended Wrapper Order

For function/model wrapper behavior, first registered is outermost. The recommended stack is:

```text
RetryMiddleware(FunctionTimeoutMiddleware(ErrorFormattingMiddleware(core)))
```

That means retry wraps timeout, and error formatting sits closest to the core call.

```csharp
using HPD.Agent;
using HPD.Agent.ErrorHandling;
using HPD.Agent.Middleware.Function;
using HPD.Agent.Providers.OpenAI;

var errorHandling = new ErrorHandlingConfig
{
    ModelRetries = new()
    {
        MaxRetries = 2
    },
    FunctionRetries = new()
    {
        MaxRetries = 2,
        RetryDelay = TimeSpan.FromSeconds(1)
    },
    SingleFunctionTimeout = TimeSpan.FromSeconds(10),
    IncludeDetailedErrorsInChat = false
};

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithMiddleware(new RetryMiddleware(errorHandling))
    .WithMiddleware(new FunctionTimeoutMiddleware(errorHandling.SingleFunctionTimeout!.Value))
    .WithMiddleware(new ErrorFormattingMiddleware(errorHandling))
    .BuildAsync();
```

Functions opt into automatic retry explicitly:

```csharp
[AIFunction(Replay = ReplayPolicy.Idempotent)]
public string LookUpOrder(string id) => orders.Find(id);
```

Omitting `Replay` also means `Never`; unsafe functions need no annotation.
Write `Never` explicitly only when overriding an inherited or function-level
safe policy.

Retry registration is explicit. A configured budget does not install retry middleware, and a positive budget does not make a function replay-safe. A function must resolve to `ReplayPolicy.Idempotent` through its function or action declaration, and application exclusions always win. When these wrappers are enabled, retry should wrap timeout, and formatting should be innermost for exhausted function errors and sanitized model errors.

The builder extension helpers are useful for explicit stacks:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithRetry(config =>
    {
        config.FunctionRetries.MaxRetries = 2;
        config.FunctionRetries.RetryDelay = TimeSpan.FromSeconds(1);
        config.IncludeDetailedErrorsInChat = false;
    })
    .WithFunctionTimeout(TimeSpan.FromSeconds(10))
    .WithErrorFormatting()
    .BuildAsync();
```

Manual extension registration uses the generic provider error handler. Hosts that need provider-specific classification can construct `RetryMiddleware` with their provider handler while preserving the same wrapper order.

## Retry Events

`RetryMiddleware` wraps both model streaming and function calls.

Function retries emit `FunctionRetryEvent`. Model-call retries emit `ModelCallRetryEvent`.

For streaming UIs, handle `ModelCallRetryEvent` carefully. The event can arrive after partial assistant text has already been displayed. A UI should clear or mark the partial text before showing fresh retry content, otherwise users may see text from a failed attempt mixed with text from the retry.

```csharp
using var retryEvents = agent.Subscribe<ModelCallRetryEvent>(evt =>
{
    Console.WriteLine(
        $"Model retry {evt.Attempt}/{evt.MaxRetries} after {evt.Delay.TotalMilliseconds:N0} ms");

    // Streaming UIs should clear or mark partial assistant text here.
});
```

Function retry events are simpler because the function result is not streamed token-by-token:

```csharp
using var functionRetries = agent.Subscribe<FunctionRetryEvent>(evt =>
{
    Console.WriteLine(
        $"{evt.FunctionName} retry {evt.Attempt}/{evt.MaxRetries}: {evt.ExceptionType}");
});
```

Function retries require effective `ReplayPolicy.Idempotent` metadata, no matching exclusion, and an available function budget. Model retries use `ModelRetries` independently. Each scope can configure a custom strategy; otherwise retry asks the provider error handler for classification and delay, including retry-after guidance when available, then falls back to exponential backoff with jitter. Non-retryable errors, exclusions, and exhausted budgets propagate.

Exclusions use exact ordinal names and always win over metadata:

```csharp
config.ErrorHandling.FunctionRetries.Exclude = new()
{
    Harnesses = ["ComputerHarness"],
    Functions = ["SendPayment"],
    Actions =
    [
        new ActionRetryExclusion
        {
            Function = "Files",
            Action = "delete"
        }
    ]
};
```

An exclusion does not grant permission, change timeout behavior, or affect model retries.

## Function Timeout

`FunctionTimeoutMiddleware` wraps function execution and enforces the configured single-function timeout. It is a function wrapper, so ordering matters.

Function timeouts are opt-in. `ErrorHandlingConfig.SingleFunctionTimeout` defaults to `null`, so ordinary functions—including synchronous subagent tools—run until they complete or the caller cancels them. Set `SingleFunctionTimeout` or call `.WithFunctionTimeout(...)` when an application needs a deadline. The parameterless `.WithFunctionTimeout()` helper uses the configured value when present and otherwise applies its explicit 30-second convenience default.

Place timeout inside retry if you want each retry attempt to receive its own timeout.

Timeout only wraps function execution. It does not impose a whole conversation timeout and it does not cancel model streaming.

## Error Formatting

`ErrorFormattingMiddleware` formats function exceptions in `AfterFunctionAsync` and sanitizes model-call errors in a model streaming wrapper.

Set `IncludeDetailedErrorsInChat = false` for production-facing agents unless the tool errors are already sanitized. With detailed errors disabled, function errors sent back into the model are generic. The original exception remains available to middleware context and observability paths.

`ErrorHandlingConfig.IncludeDetailedErrorsInChat` defaults to `true`. With detailed
errors enabled, exception messages may be exposed to the model and user. Set the
option to `false` for production-facing agents when tool exception text can contain
secrets, filesystem paths, connection strings, or internal implementation details.

For model-call errors, formatting rethrows an `InvalidOperationException` with either a sanitized provider-aware message or a detailed message, depending on the same setting.

## Iteration Safety Middleware

Error tracking, total error threshold, and circuit breaker middleware are iteration/tool safety policies. They use middleware state to track failures and can skip work or terminate runs according to policy.

These policies should use `UpdateMiddlewareState<TState>(...)` for single-state updates and `UpdateState(...)` when changing core loop state such as termination:

```csharp
context.UpdateState(state => state with
{
    IsTerminated = true,
    TerminationReason = "Circuit breaker opened"
});
```

Common builder helpers:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithCircuitBreaker(maxConsecutiveCalls: 3)
    .WithErrorTracking(maxConsecutiveErrors: 3)
    .BuildAsync();
```

Use circuit breaker for repeated identical tool calls. Use error tracking for consecutive tool failures. Use total error threshold when the aggregate error count across a run should stop further work.

## OnErrorAsync Scope

Use `OnErrorAsync` as the single terminal error observer. Each exception is
reported once, even when it crosses multiple execution boundaries.

| `ErrorSource` | Failure boundary |
| --- | --- |
| `ModelCall` | Provider resolution, model request, or final model-stream failure |
| `ToolCall` | Function-body failure |
| `Iteration` | Iteration hook, function-wrapper, or agent-loop processing failure |
| `MessageTurn` | Turn preparation, turn hook, finalization, or persistence failure |
| `Input` | Input dispatch or input middleware/handler failure |
| `Runtime` | Runtime start, stop, or resource lifecycle failure |
| `ThreadFork` | Fork preparation or `BeforeThreadForkCommitAsync` failure |

`ErrorContext.RunConfig` exposes the run configuration when available. Source flags
such as `IsModelError`, `IsInputError`, and `IsThreadForkError` avoid string or enum
checks in common handlers.

`OnErrorAsync` runs in reverse middleware registration order. Exceptions thrown by
an error handler are swallowed so they cannot replace the original failure. The
original exception continues to propagate after all handlers complete.

## Common Errors

Use these symptoms to pick the fix:

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Users see partial text followed by unrelated retry text | UI ignored `ModelCallRetryEvent` | Clear or mark the partial response when the event arrives. |
| Function timeout retries take longer than expected | Timeout is inside retry, so each attempt gets a full timeout or the tool is not eligible for retry | Lower `SingleFunctionTimeout`, lower `FunctionRetries.MaxRetries`, or add an application-level deadline. |
| Sensitive exception text appears in chat | Detailed errors are enabled or the tool returns sensitive strings itself | Set `IncludeDetailedErrorsInChat = false` and sanitize tool return values. |
| `OnErrorAsync` receives a formatted model error | `ErrorFormattingMiddleware` finished before the terminal error boundary | Inspect `ErrorContext.Error.InnerException` when the original provider exception is required. |
| Retry policy ignores provider-specific behavior | Middleware was manually added without a provider handler | Register through builder composition that supplies the provider error handler when provider-specific parsing matters. |

## What Not To Overclaim

Error middleware improves recovery and presentation; it is not a transactional guarantee. A function that partially mutates an external system before throwing may be retried, so write non-idempotent tools with their own idempotency keys or confirmation boundaries.

Timeout cancellation depends on the function honoring cancellation or completing the awaited task. It prevents the middleware from waiting past the timeout, but it cannot guarantee cleanup of external side effects.

The snippets above are source-checked example candidates. They have not been clean-compiled in a separate consumer project.
