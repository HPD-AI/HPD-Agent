using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Text.Json;

/// <summary>
/// Abstract base class for conversation message storage with cache-aware history reduction
/// and token tracking capabilities.
///
/// Key Features:
/// - Cache-aware reduction: Checks for existing summary markers to avoid redundant work
/// - Token counting: Tracks both provider-accurate and estimated token counts
/// - Template Method pattern: Derived classes implement storage backend (in-memory, database, etc.)
/// - System message preservation: Always keeps system messages at the beginning
///
/// Architecture:
/// This inherits from Microsoft.Agents.AI.ChatMessageStore and provides:
/// 1. Cache-aware reduction logic (checks __summary__ markers) - SHARED across all implementations
/// 2. Token counting methods - SHARED across all implementations
/// 3. Abstract storage methods - IMPLEMENTED by derived classes
///
/// Derived classes must implement:
/// - LoadMessagesAsync(): Load all messages from storage
/// - SaveMessagesAsync(): Replace all messages in storage
/// - AppendMessageAsync(): Add a single message to storage
/// - ClearAsync(): Remove all messages from storage
///
/// We DON'T use Microsoft's ChatReducerTriggerEvent because it's too simplistic
/// for the cache-aware architecture where reduction is applied by Conversation
/// based on metadata from Agent.
/// </summary>
public abstract class ConversationMessageStore : ChatMessageStore
{
    /// <summary>
    /// Metadata key for identifying summary messages.
    /// Matches HistoryReductionConfig.SummaryMetadataKey constant.
    /// </summary>
    protected const string SummaryMetadataKey = "__summary__";

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

    #region Cache-Aware History Reduction (Shared Logic - Works for ALL Storage Backends)

    /// <summary>
    /// Apply history reduction by removing old messages and inserting a summary.
    /// This method implements the cache-aware reduction algorithm and works
    /// with ANY storage backend (in-memory, database, Redis, etc.).
    ///
    /// Algorithm:
    /// 1. Load messages from storage (in-memory, database, wherever)
    /// 2. Preserve system messages at the beginning
    /// 3. Remove 'removedCount' messages after system messages
    /// 4. Insert summary message right after system messages
    /// 5. Save back to storage
    ///
    /// Example:
    /// Before: [System] [M1] [M2] ... [M30] [M31] ... [M50]
    /// After reduction (removedCount=30, summary="..."):
    ///   After: [System] [Summary] [M31] ... [M50]
    /// </summary>
    /// <param name="summaryMessage">Summary message to insert (must have __summary__ marker)</param>
    /// <param name="removedCount">Number of messages that were removed by the reducer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public virtual async Task ApplyReductionAsync(
        ChatMessage summaryMessage,
        int removedCount,
        CancellationToken cancellationToken = default)
    {
        // Validation
        if (summaryMessage == null)
            throw new ArgumentNullException(nameof(summaryMessage));

        if (removedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(removedCount), "Must be greater than zero");

        // Verify summary message has the marker
        if (summaryMessage.AdditionalProperties?.ContainsKey(SummaryMetadataKey) != true)
        {
            throw new InvalidOperationException(
                $"Summary message must have '{SummaryMetadataKey}' marker in AdditionalProperties");
        }

        // Load messages from storage (in-memory, database, wherever)
        var messages = await LoadMessagesAsync(cancellationToken);

        // Preserve system messages at the beginning
        int systemMsgCount = messages.Count(m => m.Role == ChatRole.System);

        // Validate we have enough messages to remove
        int availableToRemove = messages.Count - systemMsgCount;
        if (removedCount > availableToRemove)
        {
            throw new InvalidOperationException(
                $"Cannot remove {removedCount} messages. Only {availableToRemove} non-system messages available.");
        }

        // Apply reduction: remove old messages, insert summary
        messages.RemoveRange(systemMsgCount, removedCount);
        messages.Insert(systemMsgCount, summaryMessage);

        // Save back to storage (in-memory, database, wherever)
        await SaveMessagesAsync(messages, cancellationToken);
    }

    /// <summary>
    /// Checks if the message store contains a summary message.
    /// Used for cache-aware reduction detection.
    /// </summary>
    public virtual async Task<bool> HasSummaryAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        return messages.Any(m => m.AdditionalProperties?.ContainsKey(SummaryMetadataKey) == true);
    }

    /// <summary>
    /// Gets the index of the last summary message in the store.
    /// Returns -1 if no summary exists.
    /// Used by cache-aware reduction to determine if re-summarization is needed.
    /// </summary>
    public virtual async Task<int> GetLastSummaryIndexAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        return messages.FindLastIndex(m => m.AdditionalProperties?.ContainsKey(SummaryMetadataKey) == true);
    }

    /// <summary>
    /// Gets messages that appear after the last summary.
    /// If no summary exists, returns all non-system messages.
    /// Used for cache-aware reduction threshold checks.
    /// </summary>
    public virtual async Task<IReadOnlyList<ChatMessage>> GetMessagesAfterLastSummaryAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        var lastSummaryIndex = messages.FindLastIndex(m => m.AdditionalProperties?.ContainsKey(SummaryMetadataKey) == true);

        if (lastSummaryIndex >= 0)
        {
            // Return messages after the summary
            return messages.Skip(lastSummaryIndex + 1).ToList().AsReadOnly();
        }

        // No summary - return all non-system messages
        var systemMsgCount = messages.Count(m => m.Role == ChatRole.System);
        return messages.Skip(systemMsgCount).ToList().AsReadOnly();
    }

    /// <summary>
    /// Counts how many messages exist after the last summary.
    /// Used by cache-aware reduction to decide if re-reduction is needed.
    /// </summary>
    public virtual async Task<int> CountMessagesAfterLastSummaryAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        var lastSummaryIndex = messages.FindLastIndex(m => m.AdditionalProperties?.ContainsKey(SummaryMetadataKey) == true);

        if (lastSummaryIndex >= 0)
        {
            // Count messages after summary (excluding the summary itself)
            return messages.Count - lastSummaryIndex - 1;
        }

        // No summary - count all messages
        return messages.Count;
    }

    #endregion

    #region Token Counting (Shared Logic - Works for ALL Storage Backends)

    /// <summary>
    /// Calculates the total token count for all messages in the store.
    /// Uses provider-accurate counts when available, estimates otherwise.
    /// </summary>
    public virtual async Task<int> GetTotalTokenCountAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        return messages.CalculateTotalTokens();
    }

    /// <summary>
    /// Calculates token count for messages after the last summary.
    /// Used for token-aware cache checking (Phase 2 when implemented).
    /// </summary>
    public virtual async Task<int> GetTokenCountAfterLastSummaryAsync(CancellationToken cancellationToken = default)
    {
        var messagesAfterSummary = await GetMessagesAfterLastSummaryAsync(cancellationToken);
        return messagesAfterSummary.CalculateTotalTokens();
    }

    /// <summary>
    /// Gets detailed token statistics for the message store.
    /// Useful for debugging and monitoring context window usage.
    /// </summary>
    public virtual async Task<TokenStatistics> GetTokenStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var messages = await LoadMessagesAsync(cancellationToken);
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
        var lastSummaryIndex = messages.FindLastIndex(m => m.AdditionalProperties?.ContainsKey(SummaryMetadataKey) == true);
        var messagesAfterSummary = await GetMessagesAfterLastSummaryAsync(cancellationToken);

        return new TokenStatistics
        {
            TotalMessages = messages.Count,
            TotalTokens = messages.CalculateTotalTokens(),
            SystemMessageCount = systemMessages.Count,
            SystemMessageTokens = systemMessages.CalculateTotalTokens(),
            HasSummary = lastSummaryIndex >= 0,
            MessagesAfterSummary = messagesAfterSummary.Count,
            TokensAfterSummary = messagesAfterSummary.CalculateTotalTokens()
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
    public bool HasSummary { get; init; }
    public int MessagesAfterSummary { get; init; }
    public int TokensAfterSummary { get; init; }
}
