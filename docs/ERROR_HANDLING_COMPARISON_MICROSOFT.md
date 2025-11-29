# Error Handling Architecture: HPD-Agent vs Microsoft.Agents.AI

## Executive Summary

**HPD-Agent** and **Microsoft.Agents.AI** (built on Microsoft.Extensions.AI) take fundamentally different architectural approaches to error handling:

| Aspect | HPD-Agent | Microsoft.Agents.AI |
|--------|-----------|---------------------|
| **Philosophy** | Built-in, opinionated error handling | Bring-your-own via middleware pipeline |
| **Retry Logic** | Provider-aware, automatic, 3-tier priority | No built-in retry (delegate to Polly) |
| **Provider Intelligence** | Per-provider error handlers (OpenAI, Anthropic, etc.) | Generic, provider-agnostic |
| **Complexity Level** | Zero-config → Advanced customization | Manual configuration required |
| **Error Classification** | 7 error categories with smart routing | None (delegate to IChatClient middleware) |
| **Retry-After Respect** | Built-in parsing and honoring | Manual implementation needed |
| **DX Defaults** | "It just works" out-of-the-box | Requires explicit setup |

---

## Architecture Deep Dive

### **Microsoft.Agents.AI / Microsoft.Extensions.AI**

#### **Core Principle: Middleware Pipeline**

Microsoft's approach is based on **delegating** error handling to the IChatClient middleware pipeline using the **builder pattern**:

```csharp
// Microsoft's approach: Explicit middleware configuration
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(/* Add retry middleware - Polly, custom, etc. */)
    .Use(/* Add logging middleware */)
    .Use(/* Add caching middleware */)
    .Build();

var agent = chatClient.CreateAIAgent(instructions: "You are a helpful assistant");
```

**Error Handling in FunctionInvokingChatClient:**

[Source: FunctionInvokingChatClient.cs:186-217](../Reference/extensions/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs#L186-L217)

```csharp
public class FunctionInvokingChatClient
{
    /// <summary>
    /// Gets or sets a value indicating whether detailed exception information should be included
    /// in the chat history when calling the underlying IChatClient.
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } // Default: false

    /// <summary>
    /// Gets or sets the maximum number of consecutive iterations that are allowed to fail with an error.
    /// Default value is 3.
    /// </summary>
    /// <remarks>
    /// When function invocations fail with an exception, the FunctionInvokingChatClient
    /// continues to make requests to the inner client, optionally supplying exception information.
    /// This allows the IChatClient to recover from errors by trying other function parameters.
    ///
    /// If the value is set to zero, all function calling exceptions immediately terminate the function
    /// invocation loop and the exception will be rethrown to the caller.
    /// </remarks>
    public int MaximumConsecutiveErrorsPerRequest { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of iterations per request. Default value is 40.
    /// </summary>
    public int MaximumIterationsPerRequest { get; set; } = 40;

    /// <summary>
    /// Gets or sets a value indicating whether a request to call an unknown function should
    /// terminate the function calling loop. Default is false.
    /// </summary>
    public bool TerminateOnUnknownCalls { get; set; }
}
```

**What Microsoft DOES Handle:**
- ✅ Consecutive error tracking (iteration-level)
- ✅ Unknown function call handling
- ✅ Approval-required functions
- ✅ Error message sanitization (IncludeDetailedErrors)
- ✅ Function invocation context tracking

**What Microsoft DOES NOT Handle:**
- ❌ Retry logic for transient errors
- ❌ Rate limit detection and handling
- ❌ Retry-After header parsing
- ❌ Provider-specific error classification
- ❌ Exponential backoff strategies
- ❌ Per-error-category retry limits

**Microsoft's Expectation:**
> Users should add retry middleware (e.g., Polly) to the IChatClient pipeline for retry handling.

```csharp
// Example with Polly (hypothetical - user must implement)
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(innerClient => new ResilienceChatClient(innerClient, retryPolicy))
    .Use(innerClient => new FunctionInvokingChatClient(innerClient))
    .Build();
```

---

### **HPD-Agent**

#### **Core Principle: Built-In Provider Intelligence**

HPD-Agent embeds error handling **inside** the agent execution pipeline with provider-aware retry logic:

```csharp
// HPD-Agent: Zero configuration needed
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithPlugin<WeatherPlugin>()
    .BuildAsync();

// ✅ Automatically handles:
// - Rate limits (429) with Retry-After parsing
// - Server errors (5xx) with exponential backoff
// - Transient network errors
// - Provider-specific error patterns
```

**Error Handling Architecture:**

[Source: AgentConfig.cs:528-626](../HPD-Agent/Agent/AgentConfig.cs#L528-L626)

```csharp
public class ErrorHandlingConfig
{
    // Basic settings
    public bool NormalizeErrors { get; set; } = true;
    public bool IncludeProviderDetails { get; set; } = false;
    public bool IncludeDetailedErrorsInChat { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan? SingleFunctionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    // Provider-aware settings
    public bool UseProviderRetryDelays { get; set; } = true;
    public bool AutoRefreshTokensOn401 { get; set; } = true;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; set; } = 2.0;

    // Advanced customization
    public Dictionary<ErrorCategory, int>? MaxRetriesByCategory { get; set; }
    public IProviderErrorHandler? ProviderHandler { get; set; }
    public Func<Exception, int, CancellationToken, Task<TimeSpan?>>? CustomRetryStrategy { get; set; }
}
```

**Error Classification:**

[Source: ErrorCategory.cs](../HPD-Agent/ErrorHandling/ErrorCategory.cs)

```csharp
public enum ErrorCategory
{
    Unknown,              // Conservative retry
    Transient,           // Network glitches - retry with backoff
    RateLimitRetryable,  // 429 with Retry-After - retry after delay
    RateLimitTerminal,   // Quota exceeded - don't retry
    ClientError,         // 400 - don't retry (bad request)
    AuthError,           // 401 - special handling needed
    ContextWindow,       // Token limit exceeded - don't retry
    ServerError          // 5xx - retry with backoff
}
```

**3-Tier Retry Priority System:**

[Source: FunctionRetryExecutor.ExecuteWithRetryAsync](../HPD-Agent/Agent/Agent.cs#L5361-L5500)

```
┌─────────────────────────────────────────────────────────┐
│ PRIORITY 1: Custom Retry Strategy                      │
│ If user provided CustomRetryStrategy delegate          │
│ → Use it (full control)                                │
└─────────────────────────────────────────────────────────┘
                        ↓ (if null)
┌─────────────────────────────────────────────────────────┐
│ PRIORITY 2: Provider-Aware Handling                    │
│ 1. Parse exception with ProviderHandler                │
│ 2. Check per-category retry limits                     │
│ 3. Get provider-calculated delay:                      │
│    - Use RetryAfter if present                         │
│    - Use exponential backoff with provider settings    │
│    - Return null if error is non-retryable             │
└─────────────────────────────────────────────────────────┘
                        ↓ (if null)
┌─────────────────────────────────────────────────────────┐
│ PRIORITY 3: Exponential Backoff (Fallback)             │
│ delay = base * 2^attempt * random(0.9-1.1)             │
│ Apply MaxRetryDelay cap                                │
└─────────────────────────────────────────────────────────┘
```

**Provider-Specific Error Handlers:**

Each provider has a custom handler (OpenAIErrorHandler, AnthropicErrorHandler, etc.):

```csharp
public interface IProviderErrorHandler
{
    // Parse provider-specific error details from exception
    ProviderErrorDetails? ParseError(Exception exception);

    // Calculate retry delay based on attempt number and error type
    TimeSpan? GetRetryDelay(
        ProviderErrorDetails details,
        int attempt,
        TimeSpan initialDelay,
        double multiplier,
        TimeSpan maxDelay);

    // Check if error requires special handling (e.g., token refresh)
    bool RequiresSpecialHandling(ProviderErrorDetails details);
}
```

**Middleware Integration:**

HPD-Agent **also** has middleware, but it's complementary to built-in error handling:

- **ErrorTrackingMiddleware**: Tracks consecutive errors **across iterations** (not just retries within a single function)
- **CircuitBreakerMiddleware**: Detects stuck loops
- **HistoryReductionMiddleware**: Handles context window errors

---

## Developer Experience (DX) Comparison

### Scenario 1: Rate Limit Handling

#### **Microsoft.Agents.AI**

```csharp
// User must implement retry logic manually or use Polly

// Option 1: Manual retry in custom middleware
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(async (messages, options, innerClient, ct) =>
    {
        int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await innerClient.GetResponseAsync(messages, options, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt >= maxRetries) throw;

                // Parse Retry-After header manually
                var retryAfter = ParseRetryAfterHeader(ex);
                await Task.Delay(retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }
    })
    .Build();

// Option 2: Use Polly (requires additional package)
var resiliencePipeline = new ResiliencePipelineBuilder<ChatResponse>()
    .AddRetry(new RetryStrategyOptions<ChatResponse>
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    })
    .Build();

var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(innerClient => new PollyResilienceChatClient(innerClient, resiliencePipeline))
    .Build();
```

**Lines of Code:** ~30-50 (depending on approach)

#### **HPD-Agent**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .BuildAsync();

// ✅ Rate limits handled automatically with Retry-After parsing
```

**Lines of Code:** 3

**What Happens Automatically:**
1. OpenAI returns 429 with `Retry-After: 2.5s`
2. `OpenAIErrorHandler` parses the header
3. Waits exactly 2.5 seconds
4. Retries the request
5. ✅ Success!

---

### Scenario 2: Per-Error-Category Retry Limits

#### **Microsoft.Agents.AI**

```csharp
// Not directly supported - requires custom middleware
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(async (messages, options, innerClient, ct) =>
    {
        int rateLimitRetries = 5;
        int serverErrorRetries = 2;

        // Complex custom logic to classify errors and retry accordingly
        // ... (50+ lines of code)
    })
    .Build();
```

**Complexity:** High (manual error classification required)

#### **HPD-Agent**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config =>
    {
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5,
            [ErrorCategory.ServerError] = 2,
            [ErrorCategory.ClientError] = 0
        };
    })
    .BuildAsync();
```

**Complexity:** Low (declarative configuration)

---

### Scenario 3: Custom Business Logic

#### **Microsoft.Agents.AI**

```csharp
// Full control via middleware pipeline
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(async (messages, options, innerClient, ct) =>
    {
        try
        {
            return await innerClient.GetResponseAsync(messages, options, ct);
        }
        catch (Exception ex)
        {
            // Custom business logic
            if (IsQuotaExceeded(ex) && IsBusinessHours())
            {
                await SwitchToBackupProviderAsync();
                // Retry on new provider
            }
            throw;
        }
    })
    .Build();
```

**Flexibility:** ✅ Maximum flexibility
**Complexity:** ⚠️ Requires understanding middleware pipeline

#### **HPD-Agent**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config =>
    {
        config.CustomRetryStrategy = async (exception, attempt, ct) =>
        {
            var handler = new OpenAIErrorHandler();
            var details = handler.ParseError(exception);

            if (details?.Category == ErrorCategory.RateLimitTerminal && IsBusinessHours())
            {
                await SwitchToBackupProviderAsync();
                return TimeSpan.Zero; // Retry immediately on new provider
            }

            return null; // Defer to default handler
        };
    })
    .BuildAsync();
```

**Flexibility:** ✅ High flexibility with provider intelligence
**Complexity:** ⚠️ Moderate (leverages built-in error classification)

---

## Feature Comparison Matrix

| Feature | HPD-Agent | Microsoft.Agents.AI | Winner |
|---------|-----------|---------------------|--------|
| **Zero-Config Error Handling** | ✅ Auto-detects provider | ❌ Requires manual setup | **HPD** |
| **Retry-After Header Parsing** | ✅ Built-in for all providers | ❌ Manual implementation | **HPD** |
| **Provider-Specific Error Classification** | ✅ Per-provider handlers | ❌ Generic exceptions | **HPD** |
| **Error Category Classification** | ✅ 7 categories | ❌ None | **HPD** |
| **Per-Category Retry Limits** | ✅ Dictionary-based config | ❌ Not supported | **HPD** |
| **Exponential Backoff** | ✅ Built-in with jitter | ❌ Manual/Polly | **HPD** |
| **Function-Level Timeouts** | ✅ `SingleFunctionTimeout` | ❌ Not built-in | **HPD** |
| **Iteration-Level Error Tracking** | ✅ `ErrorTrackingMiddleware` | ✅ `MaximumConsecutiveErrorsPerRequest` | **Tie** |
| **Circuit Breaker** | ✅ `CircuitBreakerMiddleware` | ❌ Manual/Polly | **HPD** |
| **Custom Retry Strategy** | ✅ `CustomRetryStrategy` delegate | ✅ Custom middleware | **Tie** |
| **Middleware Extensibility** | ✅ `IAgentMiddleware` | ✅ `IChatClient` pipeline | **Tie** |
| **Polly Integration** | ⚠️ Possible via middleware | ✅ Standard pattern | **Microsoft** |
| **Documentation** | ✅ [569-line README](../HPD-Agent/ErrorHandling/README.md) | ⚠️ Minimal (delegate to Polly) | **HPD** |
| **AOT Compatibility** | ✅ Regex parsing, no reflection | ✅ AOT-compatible | **Tie** |
| **Observability Events** | ✅ `MaxConsecutiveErrorsExceededEvent` | ⚠️ OpenTelemetry only | **HPD** |

---

## Architectural Philosophy

### **Microsoft's Approach: "Composition Over Configuration"**

**Pros:**
- ✅ Maximum flexibility - compose any middleware
- ✅ No vendor lock-in - swap providers easily
- ✅ Standard .NET patterns (Polly, DI, etc.)
- ✅ Separation of concerns - retry is middleware concern
- ✅ Works with any IChatClient implementation

**Cons:**
- ❌ Requires deep understanding of middleware pipeline
- ❌ Verbose setup for common scenarios
- ❌ No "batteries included" retry logic
- ❌ User must implement provider-specific error parsing
- ❌ More code to maintain

**Best For:**
- Enterprise applications with existing Polly policies
- Teams with deep .NET middleware experience
- Applications requiring custom retry orchestration
- Multi-provider scenarios with provider-agnostic retry logic

---

### **HPD-Agent's Approach: "Opinionated by Default, Flexible When Needed"**

**Pros:**
- ✅ Zero-config defaults - works out of the box
- ✅ Provider intelligence built-in
- ✅ Progressive complexity (simple → advanced)
- ✅ Comprehensive error classification
- ✅ DX-focused - minimal code for common scenarios

**Cons:**
- ❌ More opinionated - less flexibility for edge cases
- ❌ Provider-specific code (but extensible via IProviderErrorHandler)
- ❌ Retry logic coupled to agent (but can be overridden)
- ❌ Learning curve for advanced customization

**Best For:**
- Rapid prototyping and development
- Teams new to agentic systems
- Applications using multiple providers (OpenAI, Anthropic, etc.)
- Scenarios requiring provider-aware retry logic
- Cost-sensitive applications (avoid wasted retries)

---

## Code Examples: Side-by-Side

### Example 1: Basic Agent with Rate Limit Handling

**Microsoft.Agents.AI:**
```csharp
// ~40 lines of code
var resiliencePipeline = new ResiliencePipelineBuilder<ChatResponse>()
    .AddRetry(new RetryStrategyOptions<ChatResponse>
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<ChatResponse>()
            .Handle<HttpRequestException>(ex =>
                ex.StatusCode == HttpStatusCode.TooManyRequests ||
                ex.StatusCode >= HttpStatusCode.InternalServerError)
    })
    .Build();

var chatClient = new OpenAIChatClient(apiKey, "gpt-4")
    .AsBuilder()
    .Use(innerClient => new PollyResilienceChatClient(innerClient, resiliencePipeline))
    .Use(innerClient => new FunctionInvokingChatClient(innerClient))
    .Build();

var agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant",
    tools: [weatherTool]);

await agent.RunAsync("What's the weather?");
```

**HPD-Agent:**
```csharp
// 5 lines of code
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithPlugin<WeatherPlugin>()
    .BuildAsync();

await agent.RunAsync("What's the weather?");
```

---

### Example 2: Custom Retry Logic with Provider Intelligence

**Microsoft.Agents.AI:**
```csharp
// ~80+ lines of code
var chatClient = new OpenAIChatClient(apiKey, "gpt-4")
    .AsBuilder()
    .Use(async (messages, options, innerClient, ct) =>
    {
        int maxRetries = 3;
        Dictionary<string, int> categoryRetries = new()
        {
            ["RateLimit"] = 5,
            ["ServerError"] = 2
        };

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await innerClient.GetResponseAsync(messages, options, ct);
            }
            catch (HttpRequestException ex)
            {
                var category = ClassifyError(ex); // Manual classification
                var maxForCategory = categoryRetries.GetValueOrDefault(category, maxRetries);

                if (attempt >= maxForCategory) throw;

                TimeSpan delay;
                if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Manually parse Retry-After
                    delay = ParseRetryAfterHeader(ex) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                }
                else
                {
                    delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                }

                await Task.Delay(delay, ct);
            }
        }
    })
    .Build();

// Helper methods (another ~30 lines)
string ClassifyError(HttpRequestException ex) { /* ... */ }
TimeSpan? ParseRetryAfterHeader(HttpRequestException ex) { /* ... */ }
```

**HPD-Agent:**
```csharp
// 13 lines of code
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config =>
    {
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5,
            [ErrorCategory.ServerError] = 2
        };
        config.UseProviderRetryDelays = true; // Respects Retry-After automatically
    })
    .BuildAsync();
```

---

## When to Choose Which?

### **Choose Microsoft.Agents.AI When:**
- ✅ You have existing Polly policies and want to reuse them
- ✅ You need provider-agnostic retry logic (switch providers frequently)
- ✅ You want full control over retry orchestration
- ✅ Your team has deep .NET middleware expertise
- ✅ You're building a framework/library that wraps agents

### **Choose HPD-Agent When:**
- ✅ You want rapid development with minimal boilerplate
- ✅ You're using multiple AI providers (OpenAI, Anthropic, etc.)
- ✅ You need provider-aware error handling (Retry-After, quota detection)
- ✅ You want progressive complexity (start simple, customize later)
- ✅ You're building production applications with cost sensitivity

---

## Migration Path

### From Microsoft → HPD

```csharp
// Before (Microsoft)
var chatClient = new OpenAIChatClient(apiKey, "gpt-4")
    .AsBuilder()
    .Use(/* custom retry middleware */)
    .Use(/* logging middleware */)
    .Build();

var agent = chatClient.CreateAIAgent(instructions, tools);

// After (HPD)
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithSystemInstructions(instructions)
    .WithPlugin<MyPlugin>() // tools
    .WithMiddleware(new LoggingMiddleware())
    .BuildAsync();

// ✅ Retry logic now built-in!
```

### From HPD → Microsoft

```csharp
// Before (HPD)
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config => { /* ... */ })
    .BuildAsync();

// After (Microsoft)
var chatClient = new OpenAIChatClient(apiKey, "gpt-4")
    .AsBuilder()
    .Use(/* Polly retry policy matching HPD config */)
    .Build();

var agent = chatClient.CreateAIAgent(instructions);

// ⚠️ You lose provider-aware error handling
```

---

## Conclusion

Both architectures are valid, but serve different audiences:

- **Microsoft.Agents.AI**: Maximum flexibility, standard .NET patterns, requires more code
- **HPD-Agent**: Opinionated defaults, provider intelligence, rapid development

**HPD-Agent's error handling is a *superset*** - it includes Microsoft's iteration-level tracking **plus** provider-aware retry logic with intelligent error classification.

For most developers building production agents, **HPD-Agent's approach reduces friction** and prevents common pitfalls (wasted retries, ignoring Retry-After headers, etc.).

For teams with existing resilience infrastructure (Polly policies, custom middleware), **Microsoft's approach integrates better** with existing patterns.

---

## References

### HPD-Agent
- [ErrorHandling/README.md](../HPD-Agent/ErrorHandling/README.md) - 569-line comprehensive guide
- [AgentConfig.cs:528-626](../HPD-Agent/Agent/AgentConfig.cs#L528-L626) - ErrorHandlingConfig
- [FunctionRetryExecutor](../HPD-Agent/Agent/Agent.cs#L5340-L5510) - 3-tier retry system
- [IProviderErrorHandler](../HPD-Agent/ErrorHandling/IProviderErrorHandler.cs) - Provider abstraction
- [ErrorTrackingMiddleware](../HPD-Agent/Middleware/Iteration/ErrorTrackingMiddleware.cs) - Iteration-level tracking

### Microsoft.Agents.AI
- [FunctionInvokingChatClient](../Reference/extensions/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs) - Core error handling
- [ChatClientBuilder](../Reference/extensions/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/ChatClientBuilder.cs) - Middleware pipeline
- [ChatClientAgent](../Reference/agent-framework/dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs) - Agent abstraction
