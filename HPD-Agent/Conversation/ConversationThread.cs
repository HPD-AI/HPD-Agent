using Microsoft.Extensions.AI;
using System.Text.Json;
using Microsoft.Agents.AI;

/// <summary>
/// Manages conversation state (message history, metadata, timestamps).
/// Inherits from Microsoft's AgentThread for compatibility with Agent Framework.
/// This allows one agent to serve multiple threads (conversations) concurrently.
///
/// Architecture:
/// - Uses ConversationMessageStore (which inherits from Microsoft's ChatMessageStore)
/// - Message store handles: storage, cache-aware reduction, token counting
/// - Thread handles: metadata, timestamps, display names, service integration
/// </summary>
public class ConversationThread : AgentThread
{
    private readonly ConversationMessageStore _messageStore;
    private readonly Dictionary<string, object> _metadata = new();
    private string? _serviceThreadId;

    /// <summary>
    /// Unique identifier for this conversation thread
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// When this thread was created
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Last time this thread was updated
    /// </summary>
    public DateTime LastActivity { get; private set; }

    /// <summary>
    /// Read-only view of messages in this thread.
    /// For in-memory stores this is efficient.
    /// For database stores this loads from storage.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages
    {
        get
        {
            // Get messages synchronously (for in-memory this is fast, for DB it blocks)
            return _messageStore.GetMessagesAsync().GetAwaiter().GetResult().ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Read-only view of metadata associated with this thread
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata.AsReadOnly();

    /// <summary>
    /// Number of messages in this thread.
    /// For database stores, this may require loading all messages.
    /// </summary>
    public int MessageCount
    {
        get
        {
            return Messages.Count;
        }
    }

    /// <summary>
    /// Direct access to the message store for advanced operations.
    /// Exposes cache-aware reduction and token counting capabilities.
    /// </summary>
    public ConversationMessageStore MessageStore => _messageStore;

    /// <summary>
    /// Optional service thread ID for hybrid scenarios.
    /// Enables syncing to OpenAI Assistants, Azure AI, etc.
    /// This is stored separately from the local message history.
    /// </summary>
    public string? ServiceThreadId
    {
        get => _serviceThreadId;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _serviceThreadId = value;
                LastActivity = DateTime.UtcNow;
            }
            else
            {
                _serviceThreadId = null;
            }
        }
    }

    /// <summary>
    /// Creates a new conversation thread with default in-memory storage.
    /// </summary>
    public ConversationThread()
        : this(new InMemoryConversationMessageStore())
    {
    }

    /// <summary>
    /// Creates a new conversation thread with custom message store.
    /// </summary>
    /// <param name="messageStore">Message store implementation (in-memory, database, etc.)</param>
    public ConversationThread(ConversationMessageStore messageStore)
    {
        ArgumentNullException.ThrowIfNull(messageStore);
        _messageStore = messageStore;
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a conversation thread with specific ID and message store (for deserialization).
    /// </summary>
    /// <param name="id">Thread identifier</param>
    /// <param name="createdAt">Creation timestamp</param>
    /// <param name="lastActivity">Last activity timestamp</param>
    /// <param name="messageStore">Message store implementation</param>
    private ConversationThread(string id, DateTime createdAt, DateTime lastActivity, ConversationMessageStore messageStore)
    {
        ArgumentNullException.ThrowIfNull(messageStore);
        _messageStore = messageStore;
        Id = id;
        CreatedAt = createdAt;
        LastActivity = lastActivity;
    }

    #region Message Operations (All Async)

    /// <summary>
    /// Add a single message to the thread.
    /// </summary>
    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        await _messageStore.AddMessagesAsync(new[] { message }, cancellationToken);
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Add multiple messages to the thread.
    /// </summary>
    public async Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        await _messageStore.AddMessagesAsync(messages, cancellationToken);
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Apply history reduction to the thread's message storage.
    /// Removes old messages and inserts a summary message.
    /// Delegates to ConversationMessageStore for the actual reduction logic.
    /// </summary>
    /// <param name="summaryMessage">Summary message to insert (contains __summary__ marker)</param>
    /// <param name="removedCount">Number of messages to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ApplyReductionAsync(ChatMessage summaryMessage, int removedCount, CancellationToken cancellationToken = default)
    {
        await _messageStore.ApplyReductionAsync(summaryMessage, removedCount, cancellationToken);
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Clear all messages from this thread.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _messageStore.ClearAsync(cancellationToken);
        _metadata.Clear();
        LastActivity = DateTime.UtcNow;
    }

    #endregion

    #region Metadata Operations

    /// <summary>
    /// Add metadata key/value pair to this thread.
    /// </summary>
    public void AddMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        _metadata[key] = value;
        LastActivity = DateTime.UtcNow;
    }

    #endregion

    /// <summary>
    /// Service discovery - provides AgentThreadMetadata (Microsoft pattern)
    /// </summary>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey == null && serviceType == typeof(AgentThreadMetadata))
        {
            return new AgentThreadMetadata(Id);
        }

        return base.GetService(serviceType, serviceKey);
    }

    /// <summary>
    /// Get a display name for this thread based on first user message.
    /// Useful for UI display in conversation lists.
    /// </summary>
    /// <param name="maxLength">Maximum length of display name</param>
    /// <returns>Display name or "New Conversation" if no messages</returns>
    public string GetDisplayName(int maxLength = 30)
    {
        // Check for explicit display name in metadata first
        if (_metadata.TryGetValue("DisplayName", out var name) && !string.IsNullOrEmpty(name?.ToString()))
        {
            return name.ToString()!;
        }

        // Find first user message and extract text content
        var firstUserMessage = Messages.FirstOrDefault(m => m.Role == ChatRole.User);
        if (firstUserMessage == null)
            return "New Conversation";

        var text = firstUserMessage.Text ?? string.Empty;
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    #region Serialization

    /// <summary>
    /// Serialize this thread to a JSON element (AgentThread override).
    /// Delegates message storage serialization to the message store.
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var snapshot = new ConversationThreadSnapshot
        {
            Id = Id,
            MessageStoreState = _messageStore.Serialize(jsonSerializerOptions),
            MessageStoreType = _messageStore.GetType().AssemblyQualifiedName!,
            Metadata = _metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            CreatedAt = CreatedAt,
            LastActivity = LastActivity,
            ServiceThreadId = ServiceThreadId
        };

        // Use source-generated JSON context for AOT compatibility
        return JsonSerializer.SerializeToElement(snapshot, ConversationJsonContext.Default.ConversationThreadSnapshot);
    }

    /// <summary>
    /// Deserialize a thread from a snapshot.
    /// Recreates the message store from its serialized state.
    /// </summary>
    public static ConversationThread Deserialize(ConversationThreadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Deserialize message store
        ConversationMessageStore messageStore;

        if (snapshot.MessageStoreState.HasValue && !string.IsNullOrEmpty(snapshot.MessageStoreType))
        {
            var storeType = Type.GetType(snapshot.MessageStoreType);
            if (storeType == null)
                throw new InvalidOperationException($"Cannot find type: {snapshot.MessageStoreType}");

            // Invoke constructor: new XxxMessageStore(JsonElement state, JsonSerializerOptions?)
            messageStore = (ConversationMessageStore)Activator.CreateInstance(
                storeType,
                snapshot.MessageStoreState.Value,
                (JsonSerializerOptions?)null)!;
        }
        else
        {
            // Fallback: default to in-memory if no store state
            messageStore = new InMemoryConversationMessageStore();
        }

        var thread = new ConversationThread(
            snapshot.Id,
            snapshot.CreatedAt,
            snapshot.LastActivity,
            messageStore);

        thread._serviceThreadId = snapshot.ServiceThreadId;

        foreach (var (key, value) in snapshot.Metadata)
        {
            thread._metadata[key] = value;
        }

        return thread;
    }

    #endregion

    #region AgentThread Overrides

    /// <summary>
    /// Called when new messages are received (AgentThread override).
    /// Updates this thread's message list.
    /// </summary>
    protected override async Task MessagesReceivedAsync(IEnumerable<ChatMessage> newMessages, CancellationToken cancellationToken = default)
    {
        var existingMessages = await _messageStore.GetMessagesAsync(cancellationToken);
        var messagesToAdd = newMessages.Where(m => !existingMessages.Contains(m)).ToList();

        if (messagesToAdd.Any())
        {
            await AddMessagesAsync(messagesToAdd, cancellationToken);
        }
    }

    #endregion
}

/// <summary>
/// JSON source generation context for ConversationThread serialization.
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(ConversationThreadSnapshot))]
internal partial class ConversationJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}

/// <summary>
/// Serializable snapshot of a ConversationThread for persistence.
/// Delegates message storage to the message store's own serialization.
/// </summary>
public record ConversationThreadSnapshot
{
    public required string Id { get; init; }

    /// <summary>
    /// Serialized state of the message store.
    /// Format depends on the store type:
    /// - InMemoryConversationMessageStore: Contains full message list
    /// - DatabaseConversationMessageStore: Contains just connection info + conversation ID
    /// </summary>
    public JsonElement? MessageStoreState { get; init; }

    /// <summary>
    /// Assembly-qualified type name of the message store.
    /// Used for deserialization to recreate the correct store type.
    /// </summary>
    public required string MessageStoreType { get; init; }

    public required Dictionary<string, object> Metadata { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime LastActivity { get; init; }
    public string? ServiceThreadId { get; init; }
}
