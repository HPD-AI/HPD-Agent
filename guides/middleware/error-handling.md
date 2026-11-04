# Error Handling Middleware

Error handling is split across wrapper middleware, iteration safety middleware, events, and normal application exception handling.

For UI rendering and event capture, see [Lifecycle, Retry, And Error Events](../events/lifecycle-retry-and-error-events.md).

Do not treat `OnErrorAsync` as a global catch-all. Source verification shows it is currently invoked for tool/function body exceptions. Provider/model streaming errors are handled by model-turn wrappers such as retry and error formatting. Whole message-turn failures surface through message-turn error events and the `RunAsync(...)` failure path.

## Choose The Boundary

Use the boundary that matches the failure:

| Failure | Primary mechanism | Notes |
| --- | --- | --- |
| Transient model/provider streaming error | `RetryMiddleware` model wrapper, then `ErrorFormattingMiddleware` model wrapper | `ModelCallRetryEvent` can arrive after partial text streamed. |
| Function body exception | Function wrappers, `MiddlewareErrorEvent`, `OnErrorAsync`, then `AfterFunctionAsync` formatting | This is the current `OnErrorAsync` path. |
| Slow function | `FunctionTimeoutMiddleware` | Put timeout inside retry for per-attempt timeout. |
| Repeated tool errors or loops | `ErrorTrackingMiddleware`, `TotalErrorThresholdIterationMiddleware`, `CircuitBreakerMiddleware` | These are iteration safety policies, not exception formatters. |
| Whole turn failure | `MessageTurnErrorEvent` and application `try`/`catch` around `RunAsync(...)` | Do not rely on `OnErrorAsync` here. |

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

With detailed errors enabled, exception messages may be exposed to the model and user; use that only in trusted development environments. Source comments are not fully aligned on the default, so docs should not overclaim that the default is always sanitized. Choose the setting explicitly in application configuration.

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

Use `OnErrorAsync` for tool/function body errors that flow through the function execution core.

Do not rely on it for every provider exception, model-stream exception, cancellation, or message-turn failure. Handle those with model wrappers, typed event subscriptions, and application-level `try`/`catch` around `RunAsync(...)`.

`OnErrorAsync` runs in reverse middleware registration order. Exceptions thrown by error handlers are swallowed by the pipeline so the original function exception is preserved.

## Common Errors

Use these symptoms to pick the fix:

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Users see partial text followed by unrelated retry text | UI ignored `ModelCallRetryEvent` | Clear or mark the partial response when the event arrives. |
| Function timeout retries take longer than expected | Timeout is inside retry, so each attempt gets a full timeout or the tool is not eligible for retry | Lower `SingleFunctionTimeout`, lower `FunctionRetries.MaxRetries`, or add an application-level deadline. |
| Sensitive exception text appears in chat | Detailed errors are enabled or the tool returns sensitive strings itself | Set `IncludeDetailedErrorsInChat = false` and sanitize tool return values. |
| `OnErrorAsync` did not run for provider failure | Provider/model error happened outside function execution core | Use model wrappers, retry events, and `RunAsync(...)` exception handling. |
| Retry policy ignores provider-specific behavior | Middleware was manually added without a provider handler | Register through builder composition that supplies the provider error handler when provider-specific parsing matters. |

## What Not To Overclaim

Error middleware improves recovery and presentation; it is not a transactional guarantee. A function that partially mutates an external system before throwing may be retried, so write non-idempotent tools with their own idempotency keys or confirmation boundaries.

Timeout cancellation depends on the function honoring cancellation or completing the awaited task. It prevents the middleware from waiting past the timeout, but it cannot guarantee cleanup of external side effects.

The snippets above are source-checked example candidates. They have not been clean-compiled in a separate consumer project.
