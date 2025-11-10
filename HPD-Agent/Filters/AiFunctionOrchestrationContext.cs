using Microsoft.Extensions.AI;

namespace HPD.Agent.Internal.Filters;

/// <summary>
/// Internal filter interface for function invocation pipeline.
/// Operates on FunctionInvocationContext which provides full orchestration capabilities
/// including bidirectional communication, event emission, and filter pipeline control.
/// NOT exposed to users - implementation detail for HPD-Agent internals.
/// </summary>
internal interface IAiFunctionFilter
{
    Task InvokeAsync(
        HPD.Agent.FunctionInvocationContext context,
        Func<HPD.Agent.FunctionInvocationContext, Task> next);
}

/// <summary>
/// Represents a function call request from the LLM.
/// Internal class for HPD-Agent internals.
/// </summary>
internal class ToolCallRequest
{
    public required string FunctionName { get; set; }
    public required IDictionary<string, object?> Arguments { get; set; }
}
