using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Text.Json;

/// <summary>
/// Abstract base class for conversation message storage with token tracking capabilities.
///
/// Key Features:
/// - Token counting: Tracks both provider-accurate and estimated token counts
/// - Template Method pattern: Derived classes implement storage backend (in-memory, database, etc.)
/// - Full history storage: Always stores complete message history (reduction is applied at runtime)
///
/// Architecture:
/// This inherits from Microsoft.Agents.AI.ChatMessageStore and provides:
/// 1. Token counting methods - SHARED across all implementations
/// 2. Abstract storage methods - IMPLEMENTED by derived classes
///
/// Derived classes must implement:
/// - LoadMessagesAsync(): Load all messages from storage
/// - SaveMessagesAsync(): Replace all messages in storage
/// - AppendMessageAsync(): Add a single message to storage
/// - ClearAsync(): Remove all messages from storage
///
/// Cache-aware history reduction is handled by HistoryReductionState in ConversationThread,
/// not by mutating the message store. Messages are reduced at runtime for LLM calls only.
/// </summary>
public abstract class ConversationMessageStore : ChatMessageStore
{
    #region Abstract Storage Methods - Derived Classes Must Implement

    /// <summary>
    /// Load all messages from storage.
    /// Implementation should return messages in chronological order (oldest first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all messages in chronological order</returns>
    protected abstract Task<List<ChatMessage>> LoadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace all messages in storage with the provided list.
    /// Used by reduction to persist the reduced message set.
    /// </summary>
    /// <param name="messages">Messages to save (in chronological order)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task SaveMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append a single message to storage.
    /// Should maintain chronological order.
    /// </summary>
    /// <param name="message">Message to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task AppendMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove all messages from storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public abstract Task ClearAsync(CancellationToken cancellationToken = default);

    #endregion

    #region ChatMessageStore Implementation

    /// <summary>
    /// Retrieves all messages in chronological order (oldest first).
    /// This is the contract method required by ChatMessageStore.
    /// Delegates to LoadMessagesAsync() implemented by derived classes.
    /// </summary>
    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        // NOTE: We DON'T apply reduction here like InMemoryChatMessageStore does
        // because our architecture uses Agent-detected reduction + Conversation-applied pattern.
        // Reduction is applied via ApplyReductionAsync() method when metadata flows back from Agent.
        return await LoadMessagesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds new messages to the store.
    /// This is the contract method required by ChatMessageStore.
    /// Delegates to AppendMessageAsync() implemented by derived classes.
    /// </summary>
    public override async Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        foreach (var message in messages)
        {
            await AppendMessageAsync(message, cancellationToken);
        }

        // NOTE: We DON'T apply reduction here like InMemoryChatMessageStore does
        // because our architecture uses Agent-detected reduction + Conversation-applied pattern.
    }

    /// <summary>
    /// Serializes the message store state to JSON.
    /// This is the contract method required by ChatMessageStore.
    /// Must be overridden by derived classes to serialize their specific state.
    /// </summary>
    public abstract override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null);

    #endregion

    #region Token Counting (Shared Logic - Works for ALL Storage Backends)

    /// <summary>
    /// Calculates the total token count for all messages in the store.
    /// Only includes tokens reported by LLM providers - no estimation.
    /// Messages without provider token counts contribute 0 to the total.
    /// </summary>
    public virtual async Task<int> GetTotalTokenCountAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        return messages.CalculateTotalTokens();
    }

    /// <summary>
    /// Gets detailed token statistics for the message store.
    /// Useful for debugging and monitoring context window usage.
    /// </summary>
    public virtual async Task<TokenStatistics> GetTokenStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();

        return new TokenStatistics
        {
            TotalMessages = messages.Count,
            TotalTokens = messages.CalculateTotalTokens(),
            SystemMessageCount = systemMessages.Count,
            SystemMessageTokens = systemMessages.CalculateTotalTokens()
        };
    }

    #endregion
}

/// <summary>
/// Token usage statistics for a message store.
/// Used for monitoring and debugging token-aware reduction.
/// </summary>
public record TokenStatistics
{
    public int TotalMessages { get; init; }
    public int TotalTokens { get; init; }
    public int SystemMessageCount { get; init; }
    public int SystemMessageTokens { get; init; }
}
