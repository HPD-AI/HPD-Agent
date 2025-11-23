using Microsoft.Extensions.AI;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Internal filter interface for modifying prompts before they're sent to the LLM
/// and optionally processing results after invocation.
/// NOT exposed to users - implementation detail for HPD-Agent internals.
/// </summary>
internal interface IPromptMiddleware
{
    /// <summary>
    /// Called before the LLM is invoked to modify messages, options, and context.
    /// </summary>
    /// <param name="context">The context containing messages, options, and properties</param>
    /// <param name="next">The next filter in the pipeline</param>
    /// <returns>The modified messages to send to the LLM</returns>
    Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next);

    /// <summary>
    /// Called after the LLM has responded to process results, extract memories, or perform learning.
    /// This is optional - the default implementation does nothing.
    /// </summary>
    /// <param name="context">The context containing request messages, response messages, and any exception</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// Use cases:
    /// - Extract and store memories from assistant responses
    /// - Update knowledge bases based on conversation outcomes
    /// - Log or audit conversation details
    /// - Analyze which context (documents, memories) was useful
    /// - Update rankings or relevance scores
    ///
    /// This method is called regardless of whether the invocation succeeded or failed.
    /// Check <see cref="PostInvokeContext.Exception"/> to determine success.
    /// </remarks>
    Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        // Default implementation: do nothing
        return Task.CompletedTask;
    }
}

