using Microsoft.Extensions.AI;

namespace HPD_Agent.Orchestration;

/// <summary>
/// Implementation of IOrchestrationContext that provides runtime services and state management
/// for orchestrators within the context of a Conversation.
/// </summary>
public class ConversationOrchestrationContext : IOrchestrationContext
{
    private readonly IReadOnlyList<Agent> _agents;
    private readonly ChatOptions? _options;
    private readonly ConversationThread _thread;
    private readonly string _orchestratorScope;
    private readonly List<BaseEvent> _events = new();

    public ConversationOrchestrationContext(
        IReadOnlyList<Agent> agents,
        ChatOptions? options,
        ConversationThread thread,
        string? orchestratorScope = null)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _options = options;
        _thread = thread ?? throw new ArgumentNullException(nameof(thread));
        _orchestratorScope = orchestratorScope ?? "orchestrator";
    }

    /// <summary>
    /// Gets all available agents for orchestration.
    /// </summary>
    public IReadOnlyList<Agent> GetAgents() => _agents;

    /// <summary>
    /// Gets a specific agent by its identifier (using agent name).
    /// </summary>
    public Agent? GetAgent(string agentId)
    {
        return _agents.FirstOrDefault(a => a.Name == agentId);
    }

    /// <summary>
    /// Gets the chat options configured for this orchestration.
    /// </summary>
    public ChatOptions? GetChatOptions() => _options;

    /// <summary>
    /// Reads orchestrator state from conversation thread metadata.
    /// Uses scoped keys to avoid conflicts with other metadata.
    /// </summary>
    public ValueTask<T?> ReadStateAsync<T>(string key, string? scope = null)
    {
        var scopedKey = BuildScopedKey(key, scope);
        var value = _thread.Metadata.TryGetValue(scopedKey, out var obj) ? (T?)obj : default;
        return ValueTask.FromResult(value);
    }

    /// <summary>
    /// Updates orchestrator state in conversation thread metadata.
    /// Uses scoped keys to avoid conflicts with other metadata.
    /// </summary>
    public ValueTask UpdateStateAsync<T>(string key, T? value, string? scope = null)
    {
        var scopedKey = BuildScopedKey(key, scope);
        
        if (value == null)
        {
            // Remove the key if value is null
            if (_thread.Metadata is Dictionary<string, object> dict)
            {
                dict.Remove(scopedKey);
            }
        }
        else
        {
            _thread.AddMetadata(scopedKey, value);
        }
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Clears all state in the specified scope by removing matching keys.
    /// </summary>
    public ValueTask ClearStateAsync(string? scope = null)
    {
        var targetScope = scope ?? _orchestratorScope;
        var prefix = $"{targetScope}:";
        
        if (_thread.Metadata is Dictionary<string, object> dict)
        {
            var keysToRemove = dict.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                dict.Remove(key);
            }
        }
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Emits an event that can be included in orchestration result streams.
    /// Events are collected and can be retrieved by the orchestrator.
    /// </summary>
    public ValueTask EmitEventAsync(BaseEvent orchestrationEvent)
    {
        _events.Add(orchestrationEvent);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets all events emitted during this orchestration context.
    /// </summary>
    public IReadOnlyList<BaseEvent> GetEmittedEvents() => _events.AsReadOnly();

    /// <summary>
    /// Gets conversation metadata including project context and other conversation-level data.
    /// </summary>
    public IReadOnlyDictionary<string, object> GetMetadata()
    {
        // Return a filtered view that excludes orchestrator state keys
        var filteredMetadata = new Dictionary<string, object>();
        var statePrefix = $"{_orchestratorScope}:";
        
        foreach (var kvp in _thread.Metadata)
        {
            if (!kvp.Key.StartsWith(statePrefix))
            {
                filteredMetadata[kvp.Key] = kvp.Value;
            }
        }
        
        return filteredMetadata;
    }

    /// <summary>
    /// Gets trace context from current activity for observability.
    /// </summary>
    public IReadOnlyDictionary<string, string>? GetTraceContext()
    {
        var activity = System.Diagnostics.Activity.Current;
        if (activity == null) return null;

        var traceContext = new Dictionary<string, string>();
        
        if (activity.TraceId != default)
        {
            traceContext["trace-id"] = activity.TraceId.ToString();
        }
        
        if (activity.SpanId != default)
        {
            traceContext["span-id"] = activity.SpanId.ToString();
        }
        
        if (activity.ParentSpanId != default)
        {
            traceContext["parent-span-id"] = activity.ParentSpanId.ToString();
        }

        return traceContext;
    }

    /// <summary>
    /// Creates a checkpoint with orchestrator-specific data.
    /// Combines conversation state with orchestrator checkpoint data.
    /// </summary>
    public ValueTask<string> CreateCheckpointAsync(Dictionary<string, object>? checkpointData = null)
    {
        var checkpointId = Guid.NewGuid().ToString("N");
        
        // Combine conversation metadata with checkpoint data
        var combinedData = new Dictionary<string, object>();
        
        // Add conversation state
        foreach (var kvp in _thread.Metadata)
        {
            combinedData[kvp.Key] = kvp.Value;
        }
        
        // Add orchestrator-specific checkpoint data
        if (checkpointData != null)
        {
            foreach (var kvp in checkpointData)
            {
                var scopedKey = BuildScopedKey($"checkpoint:{kvp.Key}");
                combinedData[scopedKey] = kvp.Value;
            }
        }
        
        // Store checkpoint metadata
        var checkpointKey = BuildScopedKey($"checkpoints:{checkpointId}");
        _thread.AddMetadata(checkpointKey, combinedData);
        
        return ValueTask.FromResult(checkpointId);
    }

    /// <summary>
    /// Builds a scoped key to avoid metadata conflicts.
    /// </summary>
    private string BuildScopedKey(string key, string? scope = null)
    {
        var targetScope = scope ?? _orchestratorScope;
        return $"{targetScope}:{key}";
    }
}