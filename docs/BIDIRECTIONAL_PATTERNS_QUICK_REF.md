# Bidirectional Patterns - Quick Reference

**Quick guide to using filters vs functions for human-in-the-loop patterns**

---

## When to Use What?

### Use a Filter When:
- ✅ You want to **enforce** a policy on ALL function calls
- ✅ Security/permissions/validation requirements
- ✅ Automatic logging/auditing
- ✅ The pattern should NOT be optional

**Examples**: PermissionFilter, ValidationFilter, AuditFilter

### Use a Function When:
- ✅ You want **LLM to decide** when to use it
- ✅ Optional human interaction
- ✅ Intelligent orchestration decisions
- ✅ The pattern should be opt-in

**Examples**: ClarificationFunction, custom HITL tools

---

## Quick Comparison

| Aspect | Filter | Function |
|--------|--------|----------|
| **When runs** | Every function call (automatic) | When LLM calls it (explicit) |
| **Registration** | `agent.AddFilter(new PermissionFilter())` | `agent.AddFunction(ClarificationFunction.Create())` |
| **Access context** | Via injected parameter `context` | Via `Agent.CurrentFunctionContext` |
| **Event emission** | `context.Emit(event)` | `context.Emit(event)` |
| **Wait for response** | `await context.WaitForResponseAsync<T>()` | `await context.WaitForResponseAsync<T>()` |
| **Infrastructure** | BidirectionalEventCoordinator | BidirectionalEventCoordinator (same!) |

---

## Filter Pattern Template

```csharp
public class MyCustomFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(
        AiFunctionContext context,
        Func<AiFunctionContext, Task> next)
    {
        var requestId = Guid.NewGuid().ToString();

        // Emit request
        context.Emit(new MyRequestEvent(requestId, ...));

        // Wait for response
        var response = await context.WaitForResponseAsync<MyResponseEvent>(
            requestId,
            timeout: TimeSpan.FromMinutes(5),
            cancellationToken);

        // Decide whether to continue
        if (response.Approved)
        {
            await next(context);  // Continue to next filter/function
        }
        else
        {
            context.Result = "Rejected";
        }
    }
}

// Register
agent.AddFilter(new MyCustomFilter());
```

---

## Function Pattern Template

```csharp
public static class MyCustomFunction
{
    public static AIFunction Create()
    {
        [Description("Ask the user for custom input")]
        async Task<string> MyFunctionAsync(
            [Description("The question to ask")] string question,
            CancellationToken cancellationToken)
        {
            // Get context
            var context = Agent.CurrentFunctionContext as AiFunctionContext;
            if (context == null)
                throw new InvalidOperationException("No execution context");

            var requestId = Guid.NewGuid().ToString();

            // Emit request (same as filter!)
            context.Emit(new MyRequestEvent(
                requestId,
                SourceName: "MyCustomFunction",
                question,
                AgentName: context.AgentName));

            // Wait for response (same as filter!)
            var response = await context.WaitForResponseAsync<MyResponseEvent>(
                requestId,
                timeout: TimeSpan.FromMinutes(5),
                cancellationToken);

            return response.Answer;
        }

        return AIFunctionFactory.Create(MyFunctionAsync, new AIFunctionFactoryOptions
        {
            Name = "AskForCustomInput",
            Description = "Ask user for custom input"
        });
    }
}

// Register
agent.AddFunction(MyCustomFunction.Create());
```

---

## Event Types Template

```csharp
// Marker interface
public interface IMyCustomEvent : IBidirectionalEvent
{
    string RequestId { get; }
}

// Request event
public record MyRequestEvent(
    string RequestId,
    string SourceName,
    string Question,
    string? AgentName = null) : InternalAgentEvent, IMyCustomEvent;

// Response event
public record MyResponseEvent(
    string RequestId,
    string SourceName,
    string Answer) : InternalAgentEvent, IMyCustomEvent;
```

---

## Handler Template

```csharp
await foreach (var evt in agent.RunAsync(query))
{
    switch (evt)
    {
        case MyRequestEvent request:
            // Show to user
            Console.WriteLine($"[{request.AgentName ?? "Agent"}] {request.Question}");
            var answer = Console.ReadLine();

            // Send response
            agent.SendFilterResponse(request.RequestId,
                new MyResponseEvent(
                    request.RequestId,
                    request.SourceName,
                    answer ?? string.Empty));
            break;
    }
}
```

---

## Real Examples

### Permission Filter (Automatic)

```csharp
// Wraps ALL dangerous functions
agent.AddFilter(new PermissionFilter(
    dangerousFunctions: ["WriteFile", "ExecuteShell", "DeleteFile"]));

// Flow:
// Agent calls WriteFile → PermissionFilter intercepts
// → Emits permission request → User approves/denies
// → Filter continues or blocks based on response
```

### Clarification Function (Opt-In)

```csharp
// LLM decides when to call
orchestrator.AddFunction(ClarificationFunction.Create());

// Flow:
// Agent receives unclear sub-agent response
// → LLM decides: "I need user input"
// → LLM calls AskUserForClarification(question)
// → Emits clarification request → User answers
// → Function returns answer → Agent continues
```

---

## Common Patterns

### Pattern 1: Enforce on Specific Functions (Filter)

```csharp
public class SelectiveFilter : IAiFunctionFilter
{
    private readonly string[] _targetFunctions;

    public async Task InvokeAsync(AiFunctionContext context, Func<...> next)
    {
        if (_targetFunctions.Contains(context.Function.Name))
        {
            // Emit request and wait for approval
            // ...
        }
        else
        {
            await next(context);  // Skip filtering
        }
    }
}
```

### Pattern 2: Optional User Confirmation (Function)

```csharp
// Agent decides when it needs user confirmation
agent.AddFunction(ConfirmationFunction.Create());

// Agent can call when uncertain:
// AskForConfirmation("Should I delete all files?")
```

### Pattern 3: Multi-Agent Clarification (Function)

```csharp
// Parent orchestrates multiple agents
orchestrator.AddFunction(agentA.AsAIFunction());
orchestrator.AddFunction(agentB.AsAIFunction());
orchestrator.AddFunction(ClarificationFunction.Create());

// When AgentA returns unclear response,
// Orchestrator calls AskUserForClarification
```

---

## Decision Tree

```
Do you need this to happen on EVERY function call?
│
├─ YES → Use Filter
│   │
│   └─ Do you need user input?
│       ├─ YES → Use BidirectionalEventCoordinator (PermissionFilter pattern)
│       └─ NO → Use simple filter (logging, validation, etc.)
│
└─ NO → Use Function
    │
    └─ Do you need user input?
        ├─ YES → Use BidirectionalEventCoordinator (ClarificationFunction pattern)
        └─ NO → Use regular AIFunction
```

---

## Key Takeaways

1. **Same Infrastructure**: Both filters and functions use `BidirectionalEventCoordinator`
2. **Different Semantics**: Filters = enforced, Functions = opt-in
3. **Same Streaming**: Both work via background drainer + polling loop
4. **Same Event Bubbling**: Both support nested agent scenarios
5. **Choose by Intent**: Enforce → Filter, Optional → Function

---

## Related Documentation

- **[BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md](BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md)** - Deep dive into the infrastructure
- **[CLARIFICATION_FUNCTION_USAGE.md](CLARIFICATION_FUNCTION_USAGE.md)** - Function pattern example
- **[FILTER_EVENTS_USAGE.md](../HPD-Agent/Filters/FILTER_EVENTS_USAGE.md)** - Filter pattern examples
- **[BIDIRECTIONAL_FILTER_DEADLOCK_FIX.md](BIDIRECTIONAL_FILTER_DEADLOCK_FIX.md)** - Why polling is required
