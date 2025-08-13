using Microsoft.Extensions.AI;

/// <summary>
/// Filter interface for processing completed conversation turns.
/// Executes after agent response and all tool calls are complete.
/// Applications implement this to capture state changes made by agent tool calls.
/// </summary>
public interface IConversationFilter
{
    /// <summary>
    /// Processes a completed conversation turn
    /// </summary>
    /// <param name="context">Context containing turn details and metadata</param>
    /// <param name="next">Next filter in the pipeline</param>
    /// <returns>Task representing the async filter operation</returns>
    Task InvokeAsync(
        ConversationFilterContext context,
        Func<ConversationFilterContext, Task> next);
}