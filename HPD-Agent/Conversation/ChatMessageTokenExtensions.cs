using Microsoft.Extensions.AI;

/// <summary>
/// Extension methods for ChatMessage token tracking.
///
/// TODO: Token tracking is not yet implemented. This requires a comprehensive Token Flow Architecture Map
/// to understand all token sources (system prompts, RAG injections, history, tool results, ephemeral context)
/// and their lifecycles. See docs/NEED_FOR_TOKEN_FLOW_ARCHITECTURE_MAP.md for details.
///
/// Current implementation: All methods return 0 or no-op. History reduction falls back to message count only.
/// </summary>
public static class ChatMessageTokenExtensions
{
    private const string InputTokensKey = "InputTokens";
    private const string OutputTokensKey = "OutputTokens";

    /// <summary>
    /// Gets the input token count for this message.
    /// TODO: Not implemented - requires Token Flow Architecture Map. Always returns 0.
    /// </summary>
    public static int GetInputTokens(this ChatMessage message)
    {
        // TODO: Token tracking not implemented - requires architecture map
        return 0;
    }

    /// <summary>
    /// Gets the output token count for this message.
    /// TODO: Not implemented - requires Token Flow Architecture Map. Always returns 0.
    /// </summary>
    public static int GetOutputTokens(this ChatMessage message)
    {
        // TODO: Token tracking not implemented - requires architecture map
        return 0;
    }

    /// <summary>
    /// Gets the total token count for this message.
    /// TODO: Not implemented - requires Token Flow Architecture Map. Always returns 0.
    /// </summary>
    public static int GetTotalTokens(this ChatMessage message)
    {
        // TODO: Token tracking not implemented - requires architecture map
        return 0;
    }

    /// <summary>
    /// Stores the input token count for this message.
    /// TODO: Not implemented - no-op until Token Flow Architecture Map is complete.
    /// </summary>
    internal static void SetInputTokens(this ChatMessage message, int tokenCount)
    {
        // TODO: Token tracking not implemented - no-op
    }

    /// <summary>
    /// Stores the output token count for this message.
    /// TODO: Not implemented - no-op until Token Flow Architecture Map is complete.
    /// </summary>
    internal static void SetOutputTokens(this ChatMessage message, int tokenCount)
    {
        // TODO: Token tracking not implemented - no-op
    }

    /// <summary>
    /// Calculates the total token count for a collection of messages.
    /// TODO: Not implemented - requires Token Flow Architecture Map. Always returns 0.
    /// </summary>
    public static int CalculateTotalTokens(this IEnumerable<ChatMessage> messages)
    {
        // TODO: Token tracking not implemented - requires architecture map
        return 0;
    }
}
