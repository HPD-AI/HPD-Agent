
using Microsoft.Extensions.AI;

/// <summary>
/// Context provided to prompt filters
/// </summary>
public class PromptMiddlewareContext
{
    public IEnumerable<ChatMessage> Messages { get; set; }
    public ChatOptions? Options { get; }
    public string AgentName { get; }
    public CancellationToken CancellationToken { get; }
    public Dictionary<string, object> Properties { get; }

    public PromptMiddlewareContext(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        string agentName,
        CancellationToken cancellationToken)
    {
        Messages = messages;
        Options = options;
        AgentName = agentName;
        CancellationToken = cancellationToken;
        Properties = new Dictionary<string, object>();
    }
}

