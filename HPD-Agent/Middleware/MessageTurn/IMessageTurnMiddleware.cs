using Microsoft.Extensions.AI;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Internal filter interface for processing completed message turns.
/// Executes after agent response and all tool calls are complete.
/// NOT exposed to users - implementation detail for HPD-Agent internals.
/// </summary>
internal interface IMessageTurnMiddleware
{
    /// <summary>
    /// Processes a completed message turn
    /// </summary>
    /// <param name="context">Context containing turn details and metadata</param>
    /// <param name="next">Next filter in the pipeline</param>
    /// <returns>Task representing the async filter operation</returns>
    Task InvokeAsync(
        MessageTurnMiddlewareContext context,
        Func<MessageTurnMiddlewareContext, Task> next);
}