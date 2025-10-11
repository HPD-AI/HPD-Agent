using Microsoft.Extensions.AI;
using System.Text.Json;
using Microsoft.Agents.AI;

/// <summary>
/// Manages conversation state (message history, metadata, timestamps).
/// Inherits from Microsoft's AgentThread for compatibility with Agent Framework.
/// This allows one agent to serve multiple threads (conversations) concurrently.
/// </summary>
public class ConversationThread : AgentThread
{
    private readonly List<ChatMessage> _messages = new();
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
    /// Read-only view of messages in this thread
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Read-only view of metadata associated with this thread
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata.AsReadOnly();

    /// <summary>
    /// Number of messages in this thread
    /// </summary>
    public int MessageCount => _messages.Count;

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
    /// Creates a new conversation thread with a generated ID
    /// </summary>
    public ConversationThread()
    {
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a conversation thread with a specific ID (for deserialization)
    /// </summary>
    /// <param name="id">Thread identifier</param>
    /// <param name="createdAt">Creation timestamp</param>
    /// <param name="lastActivity">Last activity timestamp</param>
    private ConversationThread(string id, DateTime createdAt, DateTime lastActivity)
    {
        Id = id;
        CreatedAt = createdAt;
        LastActivity = lastActivity;
    }

    /// <summary>
    /// Add a single message to the thread
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Add multiple messages to the thread
    /// </summary>
    public void AddMessages(IEnumerable<ChatMessage> messages)
    {
        _messages.AddRange(messages);
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Add metadata key/value pair to this thread
    /// </summary>
    public void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Apply history reduction to the thread's message storage.
    /// Removes old messages and inserts a summary message.
    /// </summary>
    /// <param name="summaryMessage">Summary message to insert (contains __summary__ marker)</param>
    /// <param name="removedCount">Number of messages to remove</param>
    public void ApplyReduction(ChatMessage? summaryMessage, int removedCount)
    {
        if (summaryMessage == null || removedCount <= 0)
            return;

        // Preserve system messages at the beginning
        int systemMsgCount = _messages.Count(m => m.Role == ChatRole.System);

        // Remove old messages after system messages
        _messages.RemoveRange(systemMsgCount, removedCount);

        // Insert summary right after system messages
        _messages.Insert(systemMsgCount, summaryMessage);

        LastActivity = DateTime.UtcNow;
    }

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
        var firstUserMessage = _messages.FirstOrDefault(m => m.Role == ChatRole.User);
        if (firstUserMessage == null)
            return "New Conversation";

        var text = firstUserMessage.Text ?? string.Empty;
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Serialize this thread to a JSON element (AgentThread override).
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var snapshot = new ConversationThreadSnapshot
        {
            Id = Id,
            Messages = _messages.ToList(),
            Metadata = _metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            CreatedAt = CreatedAt,
            LastActivity = LastActivity,
            ServiceThreadId = ServiceThreadId
        };

        // Use source-generated JSON context for AOT compatibility
        return JsonSerializer.SerializeToElement(snapshot, ConversationJsonContext.Default.ConversationThreadSnapshot);
    }

    /// <summary>
    /// Serialize this thread to a snapshot for direct persistence (legacy method).
    /// </summary>
    public ConversationThreadSnapshot SerializeToSnapshot()
    {
        return new ConversationThreadSnapshot
        {
            Id = Id,
            Messages = _messages.ToList(),
            Metadata = _metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            CreatedAt = CreatedAt,
            LastActivity = LastActivity,
            ServiceThreadId = ServiceThreadId
        };
    }

    /// <summary>
    /// Deserialize a thread from a snapshot
    /// </summary>
    public static ConversationThread Deserialize(ConversationThreadSnapshot snapshot)
    {
        var thread = new ConversationThread(snapshot.Id, snapshot.CreatedAt, snapshot.LastActivity);
        
        thread._serviceThreadId = snapshot.ServiceThreadId;

        foreach (var message in snapshot.Messages)
        {
            thread._messages.Add(message);
        }

        foreach (var (key, value) in snapshot.Metadata)
        {
            thread._metadata[key] = value;
        }

        return thread;
    }

    /// <summary>
    /// Called when new messages are received (AgentThread override).
    /// Updates this thread's message list.
    /// </summary>
    protected override Task MessagesReceivedAsync(IEnumerable<ChatMessage> newMessages, CancellationToken cancellationToken = default)
    {
        foreach (var message in newMessages)
        {
            if (!_messages.Contains(message))
            {
                AddMessage(message);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clear all messages from this thread (useful for testing or reset scenarios)
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        _metadata.Clear();
        LastActivity = DateTime.UtcNow;
    }
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
/// Serializable snapshot of a ConversationThread for persistence
/// </summary>
public record ConversationThreadSnapshot
{
    public required string Id { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime LastActivity { get; init; }
    public string? ServiceThreadId { get; init; }
}
