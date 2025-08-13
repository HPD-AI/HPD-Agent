using Microsoft.Extensions.Logging;


/// <summary>
/// HPD-Agent AI plugin for Memory CAG management
/// </summary>
public class AgentMemoryCagPlugin
{
    private readonly AgentMemoryCagManager _manager;
    private readonly string _agentName;
    private readonly ILogger<AgentMemoryCagPlugin>? _logger;

    public AgentMemoryCagPlugin(AgentMemoryCagManager manager, string agentName, ILogger<AgentMemoryCagPlugin>? logger = null)
    {
        _manager = manager;
        _agentName = agentName;
        _logger = logger;
    }

    [AIFunction(Description = "Create, update, or delete persistent memories that will be available in all future conversations")]
    public async Task<string> ManageMemoryAsync(
        string action,
        string memoryId,
        string title,
        string content)
    {
        action = action?.ToLowerInvariant() ?? string.Empty;
        switch (action)
        {
            case "create":
                var created = await _manager.CreateMemoryAsync(_agentName, title, content);
                return $"Created memory {created.Id}";
            case "update":
                var updated = await _manager.UpdateMemoryAsync(_agentName, memoryId, title, content);
                return $"Updated memory {updated.Id}";
            case "delete":
                await _manager.DeleteMemoryAsync(_agentName, memoryId);
                return $"Deleted memory {memoryId}";
            default:
                return "Unknown action. Use 'create', 'update', or 'delete'.";
        }
    }
}

