using System.Collections.Generic;
using System.Collections.Immutable;
using HPD.Agent;

/// <summary>
/// Extension methods for strongly-typed access to PromptFilterContext properties.
/// Provides discoverable, IntelliSense-friendly access to context information.
/// INTERNAL: Framework-level extensions for filter context.
/// </summary>
internal static class PromptFilterContextExtensions
{

    /// <summary>
    /// Gets the conversation ID from the filter context, if available.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>The conversation ID, or null if not available</returns>
    public static string? GetConversationId(this PromptFilterContext context)
    {
        return context.Properties.TryGetValue(PromptFilterContextKeys.ConversationId, out var value) 
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
        return context.Properties.TryGetValue(PromptFilterContextKeys.ConversationId, out var value) 
            ? value as string 
            : null;
    }

    /// <summary>
    /// Gets the conversation thread from the filter context, if available.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>The conversation thread instance, or null if not available</returns>
    public static ConversationThread? GetThread(this PromptFilterContext context)
    {
        return context.Properties.TryGetValue(PromptFilterContextKeys.Thread, out var value) 
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
        return context.Properties.TryGetValue(PromptFilterContextKeys.Thread, out var value)
            ? value as ConversationThread
            : null;
    }

    /// <summary>
    /// Gets the set of currently expanded skills.
    /// Used by SkillInstructionPromptFilter to determine which skill protocols to inject.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>Immutable set of expanded skill names, or null if not available</returns>
    public static ImmutableHashSet<string>? GetExpandedSkills(this PromptFilterContext context)
    {
        return context.Properties.TryGetValue(PromptFilterContextKeys.ExpandedSkills, out var value)
            ? value as ImmutableHashSet<string>
            : null;
    }

    /// <summary>
    /// Gets the map of skill name → instructions for active skills.
    /// Used by SkillInstructionPromptFilter to inject skill protocols into system prompt.
    /// </summary>
    /// <param name="context">The prompt filter context</param>
    /// <returns>Immutable dictionary mapping skill names to their instruction text, or null if not available</returns>
    public static ImmutableDictionary<string, string>? GetSkillInstructions(this PromptFilterContext context)
    {
        return context.Properties.TryGetValue(PromptFilterContextKeys.SkillInstructions, out var value)
            ? value as ImmutableDictionary<string, string>
            : null;
    }
}