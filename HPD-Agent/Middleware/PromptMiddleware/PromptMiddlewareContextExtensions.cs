using System.Collections.Generic;
using System.Collections.Immutable;
using HPD.Agent;

/// <summary>
/// Extension methods for strongly-typed access to PromptMiddlewareContext properties.
/// Provides discoverable, IntelliSense-friendly access to context information.
/// INTERNAL: Framework-level extensions for filter context.
/// </summary>
internal static class PromptMiddlewareContextExtensions
{

    /// <summary>
    /// Gets the conversation ID from the filter context, if available.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>The conversation ID, or null if not available</returns>
    public static string? GetConversationId(this PromptMiddlewareContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.ConversationId, out var value) 
            ? value as string 
            : null;
    }

    /// <summary>
    /// Gets the conversation ID from the post-invoke context, if available.
    /// </summary>
    /// <param name="context">The post-invoke context</param>
    /// <returns>The conversation ID, or null if not available</returns>
    public static string? GetConversationId(this PostInvokeContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.ConversationId, out var value) 
            ? value as string 
            : null;
    }

    /// <summary>
    /// Gets the conversation thread from the filter context, if available.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>The conversation thread instance, or null if not available</returns>
    public static ConversationThread? GetThread(this PromptMiddlewareContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.Thread, out var value) 
            ? value as ConversationThread 
            : null;
    }

    /// <summary>
    /// Gets the conversation thread from the post-invoke context, if available.
    /// </summary>
    /// <param name="context">The post-invoke context</param>
    /// <returns>The conversation thread instance, or null if not available</returns>
    public static ConversationThread? GetThread(this PostInvokeContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.Thread, out var value)
            ? value as ConversationThread
            : null;
    }

    /// <summary>
    /// Gets the set of currently expanded skills.
    /// Used by SkillInstructionPromptMiddleware to determine which skill protocols to inject.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>Immutable set of expanded skill names, or null if not available</returns>
    public static ImmutableHashSet<string>? GetExpandedSkills(this PromptMiddlewareContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.ExpandedSkills, out var value)
            ? value as ImmutableHashSet<string>
            : null;
    }

    /// <summary>
    /// Gets the map of skill name → instructions for active skills.
    /// Used by SkillInstructionPromptMiddleware to inject skill protocols into system prompt.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>Immutable dictionary mapping skill names to their instruction text, or null if not available</returns>
    public static ImmutableDictionary<string, string>? GetSkillInstructions(this PromptMiddlewareContext context)
    {
        return context.Properties.TryGetValue(PromptMiddlewareContextKeys.SkillInstructions, out var value)
            ? value as ImmutableDictionary<string, string>
            : null;
    }
}