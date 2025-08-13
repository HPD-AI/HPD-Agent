using Microsoft.Extensions.AI;


/// <summary>
/// Filter interface for modifying prompts before they're sent to the LLM
/// </summary>
public interface IPromptFilter
    {
        Task<IEnumerable<ChatMessage>> InvokeAsync(
            PromptFilterContext context,
            Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next);
    }

