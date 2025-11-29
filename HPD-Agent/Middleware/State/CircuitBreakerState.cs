using System.Collections.Immutable;

namespace HPD.Agent;

/// <summary>
/// State for circuit breaker tracking. Immutable record with static abstract key.
/// Tracks repeated identical function calls to detect and prevent infinite loops.
/// </summary>
/// <remarks>
/// <para><b>Thread Safety:</b></para>
/// <para>
/// This state is immutable and flows through the context.
/// It is NOT stored in middleware instance fields, preserving thread safety
/// for concurrent RunAsync() calls.
/// </para>
///
/// <para><b>Usage:</b></para>
/// <code>
/// // Read state
/// var cbState = context.State.GetState&lt;CircuitBreakerState&gt;();
/// var count = cbState.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0);
///
/// // Update state
/// context.UpdateState&lt;CircuitBreakerState&gt;(s => s with
/// {
///     LastSignaturePerTool = s.LastSignaturePerTool.SetItem(toolName, signature),
///     ConsecutiveCountPerTool = s.ConsecutiveCountPerTool.SetItem(toolName, newCount)
/// });
/// </code>
/// </remarks>
public sealed record CircuitBreakerState : IMiddlewareState
{
    /// <summary>
    /// Unique key for this middleware state type.
    /// </summary>
    public static string Key => "HPD.Agent.CircuitBreaker";

    /// <summary>
    /// Creates default/initial state with empty tracking dictionaries.
    /// </summary>
    public static IMiddlewareState CreateDefault() => new CircuitBreakerState();

    /// <summary>
    /// Last function signature per tool (for detecting identical calls).
    /// Key: Tool name
    /// Value: Signature (FunctionName(arg1=val1,arg2=val2,...))
    /// </summary>
    public Dictionary<string, string> LastSignaturePerTool { get; init; }
        = new();

    /// <summary>
    /// Consecutive identical call count per tool.
    /// Key: Tool name
    /// Value: Number of times called consecutively with identical arguments
    /// Triggers circuit breaker when threshold is exceeded.
    /// </summary>
    public Dictionary<string, int> ConsecutiveCountPerTool { get; init; }
        = new();

    /// <summary>
    /// Records a tool call, updating the signature and count tracking.
    /// </summary>
    /// <param name="toolName">Name of the tool being called</param>
    /// <param name="signature">Signature of the tool call (name + args)</param>
    /// <returns>New state with updated tracking</returns>
    public CircuitBreakerState RecordToolCall(string toolName, string signature)
    {
        var lastSig = LastSignaturePerTool.GetValueOrDefault(toolName);
        var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;

        var newLastSignature = new Dictionary<string, string>(LastSignaturePerTool) { [toolName] = signature };
        var newCount = new Dictionary<string, int>(ConsecutiveCountPerTool) { [toolName] = isIdentical ? ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1 : 1 };

        return this with
        {
            LastSignaturePerTool = newLastSignature,
            ConsecutiveCountPerTool = newCount
        };
    }

    /// <summary>
    /// Gets the predicted count for a tool if it were called with the given signature.
    /// Used for predictive circuit breaker checking before tool execution.
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <param name="signature">Signature of the tool call</param>
    /// <returns>What the count would be if this call executes</returns>
    public int GetPredictedCount(string toolName, string signature)
    {
        var lastSig = LastSignaturePerTool.GetValueOrDefault(toolName);
        var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;

        return isIdentical
            ? ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1
            : 1;
    }
}
