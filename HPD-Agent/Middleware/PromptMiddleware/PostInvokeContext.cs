using Microsoft.Extensions.AI;

/// <summary>
/// Context provided to prompt filters after LLM invocation.
/// Contains request messages, response messages, and invocation outcome.
/// </summary>
public class PostInvokeContext
{
    /// <summary>
    /// The messages that were sent to the LLM (after all pre-processing filters).
    /// </summary>
    public IEnumerable<ChatMessage> RequestMessages { get; }

    /// <summary>
    /// The messages returned by the LLM, or null if invocation failed.
    /// </summary>
    public IEnumerable<ChatMessage>? ResponseMessages { get; }

    /// <summary>
    /// Exception that occurred during invocation, or null if successful.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Additional properties shared across filters (same as PromptMiddlewareContext.Properties).
    /// Contains context like Project, ConversationId, etc.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; }

    /// <summary>
    /// The agent name that processed this request.
    /// </summary>
    public string AgentName { get; }

    /// <summary>
    /// The chat options used for this invocation.
    /// </summary>
    public ChatOptions? Options { get; }

    /// <summary>
    /// Creates a new PostInvokeContext instance.
    /// </summary>
    public PostInvokeContext(
        IEnumerable<ChatMessage> requestMessages,
        IEnumerable<ChatMessage>? responseMessages,
        Exception? exception,
        IReadOnlyDictionary<string, object> properties,
        string agentName,
        ChatOptions? options)
    {
        RequestMessages = requestMessages ?? throw new ArgumentNullException(nameof(requestMessages));
        ResponseMessages = responseMessages;
        Exception = exception;
        Properties = properties ?? new Dictionary<string, object>();
        AgentName = agentName;
        Options = options;
    }

    /// <summary>
    /// Returns true if the invocation succeeded (no exception and has response messages).
    /// </summary>
    public bool IsSuccess => Exception == null && ResponseMessages != null;

    /// <summary>
    /// Returns true if the invocation failed (exception occurred or no response messages).
    /// </summary>
    public bool IsFailure => !IsSuccess;
}
