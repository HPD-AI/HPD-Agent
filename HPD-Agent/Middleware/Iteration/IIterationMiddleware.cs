namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Middleware that runs before and after each LLM call in the agentic loop.
/// Provides access to iteration state and can modify messages/options dynamically.
/// Uses explicit lifecycle methods instead of middleware pattern due to streaming constraints.
/// </summary>
/// <remarks>
/// Iteration Middlewares complement prompt Middlewares (which run once per message turn).
/// Use iteration Middlewares when you need:
/// - Access to tool results from previous iterations
/// - Dynamic instruction modification per iteration
/// - Iteration-aware guidance
/// - Pre and post LLM call hooks
///
/// Performance Note: Iteration Middlewares run multiple times per message turn (once per LLM call).
/// Keep operations lightweight (< 1ms). For heavy operations like RAG or memory retrieval,
/// use IPromptMiddleware instead (runs once per message turn).
///
/// Architecture Note: This uses a lifecycle pattern (BeforeIterationAsync/AfterIterationAsync)
/// instead of middleware pattern because the LLM call uses yield return for streaming,
/// which cannot be wrapped in a lambda expression.
/// </remarks>
/// <example>
/// <code>
/// public class MyIterationMiddleWare : IIterationMiddleWare
/// {
///     public Task BeforeIterationAsync(
///         IterationMiddleWareContext context,
///         CancellationToken cancellationToken)
///     {
///         // Modify before LLM call
///         if (context.Iteration > 0)
///         {
///             context.Options.Instructions += "\nAnalyze the tool results and respond.";
///         }
///         return Task.CompletedTask;
///     }
///
///     public Task AfterIterationAsync(
///         IterationMiddleWareContext context,
///         CancellationToken cancellationToken)
///     {
///         // React to response after LLM call
///         if (context.IsFinalIteration)
///         {
///             Console.WriteLine("Final iteration completed");
///         }
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
internal interface IIterationMiddleWare
{
    /// <summary>
    /// Called BEFORE the LLM call begins.
    /// Middlewares can modify messages/options to inject dynamic context.
    /// Can skip the LLM call by setting context.SkipLLMCall = true.
    /// </summary>
    /// <param name="context">Iteration context with mutable messages/options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when pre-processing is done</returns>
    Task BeforeIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called AFTER LLM returns tool calls but BEFORE any tools execute.
    /// Middlewares can inspect pending tool calls and prevent execution if needed.
    /// Response and ToolCalls are populated; ToolResults is empty.
    /// </summary>
    /// <remarks>
    /// Use this hook for:
    /// - Circuit breaker: Check if tools would exceed call thresholds
    /// - Batch validation: Validate all pending tool calls before execution
    /// - Pre-execution logging: Log what tools will be called
    /// - Termination: Set SkipToolExecution = true to prevent ALL tools from running
    ///
    /// Key difference from BeforeIterationAsync:
    /// - BeforeIterationAsync: Before LLM call, ToolCalls is empty
    /// - BeforeToolExecutionAsync: After LLM call, ToolCalls is populated
    ///
    /// Key difference from AfterIterationAsync:
    /// - BeforeToolExecutionAsync: Tools haven't run yet, can prevent execution
    /// - AfterIterationAsync: Tools have run, can only react to results
    /// </remarks>
    /// <param name="context">Iteration context with populated Response and ToolCalls</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when pre-tool-execution processing is done</returns>
    Task BeforeToolExecutionAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called AFTER tool execution completes (all tools have been executed).
    /// Middlewares can inspect both LLM response and tool results to make decisions.
    /// Response, ToolCalls, ToolResults, and Exception properties are populated at this point.
    /// </summary>
    /// <remarks>
    /// Use this hook for:
    /// - Error tracking: Analyze ToolResults for failures
    /// - Logging: Record tool execution outcomes
    /// - State signaling: Set Properties to communicate with agent loop
    /// - Termination: Set SkipLLMCall and provide Response to stop execution
    /// </remarks>
    /// <param name="context">Iteration context with populated Response, ToolCalls, and ToolResults</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when post-processing is done</returns>
    Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken);
}
