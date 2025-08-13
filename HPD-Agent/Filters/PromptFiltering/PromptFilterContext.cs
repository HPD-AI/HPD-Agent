
using Microsoft.Extensions.AI;

/// <summary>
/// Context provided to prompt filters
/// </summary>
public class PromptFilterContext
{
    public IEnumerable<ChatMessage> Messages { get; set; }
    public ChatOptions? Options { get; }
    public Conversation Conversation { get; }
    public string AgentName { get; }
    public CancellationToken CancellationToken { get; }
    public Dictionary<string, object> Properties { get; }

    public PromptFilterContext(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Conversation conversation,
        string agentName,
        CancellationToken cancellationToken)
    {
        Messages = messages;
        Options = options;
        Conversation = conversation;
        AgentName = agentName;
        CancellationToken = cancellationToken;
        Properties = new Dictionary<string, object>();
    }
}

