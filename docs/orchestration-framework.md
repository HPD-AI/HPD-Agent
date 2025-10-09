# HPD-Agent Orchestration Framework

A next-generation orchestration pattern for multi-agent AI systems that combines the best practices from Microsoft AI Workflows and Pydantic Graph, while providing superior extensibility, state management, and developer experience.

**Now supports both conversation-based and generic orchestration scenarios!**

## Table of Contents

- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Quick Start](#quick-start)
- [API Reference](#api-reference)
- [Patterns & Best Practices](#patterns--best-practices)
- [Advanced Features](#advanced-features)
- [Migration Guide](#migration-guide)
- [Examples](#examples)
- [Comparison with Other Frameworks](#comparison-with-other-frameworks)

## Overview

The HPD-Agent Orchestration Framework uses a **Request + Context** pattern that cleanly separates:

- **Serializable orchestration data** (`OrchestrationRequest`) - What to orchestrate
- **Runtime services and state** (`IOrchestrationContext`) - How to orchestrate

This design enables:
- ✅ **Full serialization** for checkpointing and distributed scenarios
- ✅ **Rich extensibility** without breaking interface changes
- ✅ **Built-in state management** with scoping support
- ✅ **Superior testing** with clean mocking
- ✅ **Enterprise observability** with integrated tracing and events
- ✅ **Generic input support** for both conversation and non-conversation scenarios

## Core Concepts

### 1. OrchestrationRequest (The "What")

A fully serializable object containing all the data needed for orchestration:

```csharp
public record OrchestrationRequest
{
    // Generic input support
    public required object Input { get; init; }               // Any serializable input data
    public required string InputType { get; init; }           // "chat", "file", "data", "api", etc.
    
    // Chat-specific convenience (optional)
    public IReadOnlyList<ChatMessage>? History { get; init; } // For conversation scenarios
    
    // Core orchestration data
    public required IReadOnlyList<string> AgentIds { get; init; }
    public string? RunId { get; init; }
    public string? ConversationId { get; init; }
    
    // Business context
    public int Priority { get; init; } = 5;                    // 0-10 priority scale
    public TimeSpan? MaxExecutionTime { get; init; }          // SLA constraints
    
    // Extensibility
    public IReadOnlyDictionary<string, object> Extensions { get; init; } = new Dictionary<string, object>();
    
    // Helper methods
    public T? GetExtension<T>(string key) where T : class;
    public bool HasExtension(string key);
    public IReadOnlyList<ChatMessage>? GetChatHistory();      // Convenience for chat scenarios
    public T? GetInput<T>() where T : class;                  // Typed input access
    public bool IsConversationOrchestration { get; }          // Check if chat-based
}
```

### 2. IOrchestrationContext (The "How")

A rich service interface providing runtime objects and state management:

```csharp
public interface IOrchestrationContext
{
    // Agent management
    IReadOnlyList<Agent> GetAgents();
    Agent? GetAgent(string agentId);
    ChatOptions? GetChatOptions();
    
    // State management (scoped and persistent)
    ValueTask<T?> ReadStateAsync<T>(string key, string? scope = null);
    ValueTask UpdateStateAsync<T>(string key, T? value, string? scope = null);
    ValueTask ClearStateAsync(string? scope = null);
    
    // Observability
    ValueTask EmitEventAsync(BaseEvent orchestrationEvent);
    IReadOnlyDictionary<string, object> GetMetadata();
    IReadOnlyDictionary<string, string>? GetTraceContext();
    
    // Checkpointing
    ValueTask<string> CreateCheckpointAsync(Dictionary<string, object>? checkpointData = null);
}
```

### 3. IOrchestrator Interface

Clean interface using the Request + Context pattern:

```csharp
public interface IOrchestrator
{
    Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default);

    Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default);
}
```

## Quick Start

### 1. Conversation-Based Orchestrator (Traditional)

```csharp
public class RoundRobinOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = context.GetAgents();
        
        // Get conversation history using convenience method
        var history = request.GetChatHistory();
        if (history == null)
            throw new InvalidOperationException("This orchestrator requires conversation history");
        
        // Get last used agent from state
        var lastAgentIndex = await context.ReadStateAsync<int>("lastAgentIndex");
        var nextIndex = (lastAgentIndex + 1) % agents.Count;
        var selectedAgent = agents[nextIndex];
        
        // Save state for next turn
        await context.UpdateStateAsync("lastAgentIndex", nextIndex);
        
        // Execute agent with conversation history
        var options = context.GetChatOptions();
        var streamingResult = await selectedAgent.ExecuteStreamingTurnAsync(
            history, options, cancellationToken: cancellationToken);

        // Consume stream
        await foreach (var evt in streamingResult.EventStream.WithCancellation(cancellationToken))
        {
            await context.EmitEventAsync(evt);
        }

        var finalHistory = await streamingResult.FinalHistory;
        
        return new OrchestrationResult
        {
            Output = new ChatResponse(finalHistory),
            OutputType = "chat",
            PrimaryAgent = selectedAgent,
            RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
            Status = OrchestrationStatus.Completed,
            Metadata = new OrchestrationMetadata
            {
                StrategyName = "RoundRobin",
                DecisionDuration = TimeSpan.Zero,
                Context = OrchestrationHelpers.PackageReductionMetadata(await streamingResult.ReductionTask)
            }
        };
    }

    public async Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // Implementation similar to above but with streaming support
        // ...
    }
}
```

### 2. Generic File Processing Orchestrator (New!)

```csharp
public class FileProcessingOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // This orchestrator handles file processing, not conversations
        if (request.InputType != "file")
            throw new InvalidOperationException($"Expected 'file' input, got '{request.InputType}'");
        
        var fileData = request.GetInput<FileProcessingRequest>();
        if (fileData == null)
            throw new InvalidOperationException("Invalid file processing request");
        
        var agents = context.GetAgents();
        
        // Select agent based on file type
        var selectedAgent = fileData.FileType switch
        {
            "pdf" => agents.FirstOrDefault(a => a.Name.Contains("PDF")),
            "image" => agents.FirstOrDefault(a => a.Name.Contains("Vision")),
            "code" => agents.FirstOrDefault(a => a.Name.Contains("Code")),
            _ => agents.First()
        } ?? agents.First();
        
        // Store processing metadata
        await context.UpdateStateAsync("lastProcessedFile", fileData.FileName);
        await context.UpdateStateAsync("processedCount", 
            await context.ReadStateAsync<int>("processedCount") + 1);
        
        // Emit custom processing event
        await context.EmitEventAsync(new FileProcessingStartedEvent(fileData.FileName, fileData.FileType));
        
        // Process the file (custom agent method)
        var result = await ProcessFileWithAgent(selectedAgent, fileData, cancellationToken);
        
        return new OrchestrationResult
        {
            Output = new FileProcessingResult(processedContent, metadata),  // File-specific output
            OutputType = "file",                                            // File output type
            PrimaryAgent = selectedAgent,
            RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
            Status = OrchestrationStatus.Completed,
            Metadata = new OrchestrationMetadata
            {
                StrategyName = "FileProcessing",
                DecisionDuration = TimeSpan.Zero,
                Context = new Dictionary<string, object>
                {
                    ["fileType"] = fileData.FileType,
                    ["fileName"] = fileData.FileName,
                    ["fileSize"] = fileData.FileSize
                }
            }
        };
    }

    private async Task<ChatResponse> ProcessFileWithAgent(Agent agent, FileProcessingRequest fileData, CancellationToken cancellationToken)
    {
        // Convert file data to chat messages for agent processing
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, $"Process this {fileData.FileType} file: {fileData.FileName}"),
            new ChatMessage(ChatRole.User, fileData.Content)
        };
        
        var streamingResult = await agent.ExecuteStreamingTurnAsync(messages, cancellationToken: cancellationToken);
        
        // Consume stream
        await foreach (var _ in streamingResult.EventStream.WithCancellation(cancellationToken)) { }
        
        var finalHistory = await streamingResult.FinalHistory;
        return new ChatResponse(finalHistory);
    }

    public Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("File processing doesn't support streaming yet");
    }
}

// Custom data model for file processing
public record FileProcessingRequest(string FileName, string FileType, string Content, long FileSize);

// Custom result model for file processing
public record FileProcessingResult(string ProcessedContent, Dictionary<string, object> Metadata);

// Custom event for file processing
public record FileProcessingStartedEvent(string FileName, string FileType) : BaseEvent;
```

### 3. Using the Orchestrators

```csharp
// Conversation orchestration (traditional)
var conversationOrchestrator = new RoundRobinOrchestrator();
var conversation = new Conversation(agents, conversationOrchestrator);
var result = await conversation.SendAsync("Hello, how can you help me?");

// File processing orchestration (new!)
var fileOrchestrator = new FileProcessingOrchestrator();
var fileRequest = new OrchestrationRequest
{
    Input = new FileProcessingRequest("document.pdf", "pdf", pdfContent, 1024000),
    InputType = "file",
    AgentIds = agents.Select(a => a.Name).ToList(),
    Priority = 8  // High priority for file processing
};
var fileContext = new CustomOrchestrationContext(agents, options: null);
var fileResult = await fileOrchestrator.OrchestrateAsync(fileRequest, fileContext);
```

## API Reference

### OrchestrationRequest Properties

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `Input` | `object` | Generic input data (any serializable object) | ✅ |
| `InputType` | `string` | Type descriptor ("chat", "file", "data", etc.) | ✅ |
| `AgentIds` | `IReadOnlyList<string>` | Available agent identifiers | ✅ |
| `History` | `IReadOnlyList<ChatMessage>?` | Conversation history (optional) | ❌ |
| `RunId` | `string?` | Unique orchestration run ID | ❌ |
| `ConversationId` | `string?` | Conversation identifier | ❌ |
| `Priority` | `int` | Priority level (0-10, default: 5) | ❌ |
| `MaxExecutionTime` | `TimeSpan?` | Maximum execution time | ❌ |
| `Extensions` | `IReadOnlyDictionary<string, object>` | Custom extensions | ❌ |

#### Convenience Methods
- `GetChatHistory()` - Get conversation history (from History or Input)
- `GetInput<T>()` - Get typed input data
- `IsConversationOrchestration` - Check if this is chat-based
- `GetExtension<T>(key)` - Get typed extension value
- `HasExtension(key)` - Check if extension exists

### IOrchestrationContext Methods

#### Agent Management
- `GetAgents()` - Get all available agents
- `GetAgent(string agentId)` - Get specific agent by ID
- `GetChatOptions()` - Get chat configuration

#### State Management
- `ReadStateAsync<T>(key, scope?)` - Read scoped state
- `UpdateStateAsync<T>(key, value, scope?)` - Update scoped state  
- `ClearStateAsync(scope?)` - Clear state scope

#### Observability
- `EmitEventAsync(BaseEvent)` - Emit orchestration event
- `GetMetadata()` - Get conversation metadata
- `GetTraceContext()` - Get OpenTelemetry trace context

#### Checkpointing
- `CreateCheckpointAsync(data?)` - Create resumable checkpoint

## Patterns & Best Practices

### 1. Priority-Based Orchestration

```csharp
public class PriorityOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = context.GetAgents();
        
        Agent selectedAgent;
        
        if (request.Priority >= 9)
        {
            // High priority - use fastest agent
            selectedAgent = agents.OrderBy(GetAgentLatency).First();
            await context.EmitEventAsync(new PriorityEscalationEvent(request.Priority));
        }
        else if (request.Priority <= 2)
        {
            // Low priority - use most cost-effective agent
            selectedAgent = agents.OrderBy(GetAgentCost).First();
        }
        else
        {
            // Normal priority - use load balancing
            selectedAgent = await GetLeastLoadedAgent(agents, context);
        }
        
        // Track agent selection for analytics
        await context.UpdateStateAsync($"selections:{selectedAgent.Name}", 
            DateTime.UtcNow, "analytics");
        
        // ... continue with execution
    }
}
```

### 2. Extension-Based Configuration

```csharp
public class ConfigurableOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // Read strategy from extensions
        var strategy = request.GetExtension<string>("strategy") ?? "default";
        
        // Read user preferences
        var preferences = request.GetExtension<UserPreferences>("preferences");
        
        // Read cost constraints
        var maxCost = request.GetExtension<decimal?>("maxCost");
        
        // Apply configuration
        var agents = context.GetAgents();
        var filteredAgents = ApplyPreferences(agents, preferences);
        
        if (maxCost.HasValue)
        {
            filteredAgents = filteredAgents.Where(a => GetAgentCost(a) <= maxCost.Value);
        }
        
        var selectedAgent = strategy switch
        {
            "fastest" => filteredAgents.OrderBy(GetAgentLatency).First(),
            "cheapest" => filteredAgents.OrderBy(GetAgentCost).First(),
            "smartest" => filteredAgents.OrderByDescending(GetAgentCapability).First(),
            _ => filteredAgents.First()
        };
        
        // ... continue with execution
    }
}
```

### 3. Stateful Multi-Turn Orchestration

```csharp
public class ConversationalOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // Track conversation state
        var turnCount = await context.ReadStateAsync<int>("turnCount");
        var agentHistory = await context.ReadStateAsync<List<string>>("agentHistory") ?? new();
        var userSatisfaction = await context.ReadStateAsync<double?>("satisfaction");
        
        var agents = context.GetAgents();
        Agent selectedAgent;
        
        if (turnCount == 0)
        {
            // First turn - use general purpose agent
            selectedAgent = agents.First(a => a.Name.Contains("General"));
        }
        else if (userSatisfaction < 0.7)
        {
            // User seems unsatisfied - escalate to specialist
            selectedAgent = agents.First(a => a.Name.Contains("Specialist"));
            await context.EmitEventAsync(new EscalationEvent("Low satisfaction"));
        }
        else if (agentHistory.LastOrDefault() == "Specialist")
        {
            // Continue with specialist if they were helping
            selectedAgent = agents.First(a => a.Name.Contains("Specialist"));
        }
        else
        {
            // Normal flow - use context-appropriate agent
            selectedAgent = SelectAgentByContext(request.GetChatHistory(), agents);
        }
        
        // Update state
        await context.UpdateStateAsync("turnCount", turnCount + 1);
        agentHistory.Add(selectedAgent.Name);
        await context.UpdateStateAsync("agentHistory", agentHistory);
        
        // Create checkpoint for resumability
        await context.CreateCheckpointAsync(new Dictionary<string, object>
        {
            ["selectedAgent"] = selectedAgent.Name,
            ["reasoning"] = $"Turn {turnCount + 1}, satisfaction: {userSatisfaction}"
        });
        
        // ... continue with execution
    }
}
```

## Advanced Features

### 1. Scoped State Management

```csharp
// Different scopes for different purposes
await context.UpdateStateAsync("userPrefs", preferences, "user-scope");
await context.UpdateStateAsync("sessionData", session, "conversation-scope");
await context.UpdateStateAsync("agentMetrics", metrics, "orchestrator-scope");
await context.UpdateStateAsync("globalConfig", config, "global-scope");

// Read from specific scope
var userPrefs = await context.ReadStateAsync<UserPreferences>("userPrefs", "user-scope");

// Clear entire scope
await context.ClearStateAsync("conversation-scope");
```

### 2. Event-Driven Observability

```csharp
// Emit custom events for monitoring
await context.EmitEventAsync(new OrchestrationStartedEvent(request.Priority));
await context.EmitEventAsync(new AgentSelectionEvent(selectedAgent.Name, reasoning));
await context.EmitEventAsync(new PerformanceMetricEvent(latency, cost));
await context.EmitEventAsync(new OrchestrationCompletedEvent(success: true));

// Events are automatically included in streaming results
// and can be consumed by monitoring systems
```

### 3. Checkpointing and Resumption

```csharp
// Create detailed checkpoint
var checkpointId = await context.CreateCheckpointAsync(new Dictionary<string, object>
{
    ["currentStep"] = "agent-selection",
    ["selectedAgents"] = candidateAgents.Select(a => a.Name).ToArray(),
    ["decisionFactors"] = new { priority = request.Priority, cost = totalCost },
    ["nextActions"] = new[] { "execute-agent", "monitor-performance" }
});

// Checkpoint is automatically serializable and can be restored later
// for resuming long-running orchestrations
```

## Migration Guide

### From Old Parameter Pattern

**Before:**
```csharp
public async Task<OrchestrationResult> OrchestrateAsync(
    IReadOnlyList<ChatMessage> history,
    IReadOnlyList<Agent> agents,
    string? conversationId = null,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
{
    var selectedAgent = SelectBestAgent(history, agents);
    // ... rest of implementation
}
```

**After:**
```csharp
public async Task<OrchestrationResult> OrchestrateAsync(
    OrchestrationRequest request,
    IOrchestrationContext context,
    CancellationToken cancellationToken = default)
{
    var agents = context.GetAgents();
    var selectedAgent = SelectBestAgent(request.GetChatHistory(), agents);
    // ... rest of implementation
}
```

### Migration Steps

1. **Update interface signature** - Change parameters to request + context
2. **Extract data from request** - Get history, agent IDs, etc. from request object
3. **Get runtime objects from context** - Get agents, options from context
4. **Add state management** - Use context state methods instead of external storage
5. **Add event emission** - Use context.EmitEventAsync for observability
6. **Test thoroughly** - New pattern provides better testability

## Examples

### 1. Load-Balanced Orchestrator

```csharp
public class LoadBalancedOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = context.GetAgents();
        
        // Get current load for each agent
        var agentLoads = new Dictionary<string, int>();
        foreach (var agent in agents)
        {
            var load = await context.ReadStateAsync<int>($"load:{agent.Name}") ?? 0;
            agentLoads[agent.Name] = load;
        }
        
        // Select least loaded agent
        var selectedAgent = agents.OrderBy(a => agentLoads[a.Name]).First();
        
        // Update load tracking
        await context.UpdateStateAsync($"load:{selectedAgent.Name}", agentLoads[selectedAgent.Name] + 1);
        
        // Emit selection event
        await context.EmitEventAsync(new AgentSelectionEvent(selectedAgent.Name, $"Load: {agentLoads[selectedAgent.Name]}"));
        
        try
        {
            var options = context.GetChatOptions();
            var streamingResult = await selectedAgent.ExecuteStreamingTurnAsync(
                request.GetChatHistory() ?? throw new InvalidOperationException("Chat history required"), 
                options, cancellationToken: cancellationToken);

            await foreach (var evt in streamingResult.EventStream.WithCancellation(cancellationToken))
            {
                await context.EmitEventAsync(evt);
            }

            var finalHistory = await streamingResult.FinalHistory;
            
            return new OrchestrationResult
            {
                Response = new ChatResponse(finalHistory),
                PrimaryAgent = selectedAgent,
                RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
                Status = OrchestrationStatus.Completed,
                Metadata = new OrchestrationMetadata
                {
                    StrategyName = "LoadBalanced",
                    AgentScores = agentLoads.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value),
                    Context = OrchestrationHelpers.PackageReductionMetadata(await streamingResult.ReductionTask)
                }
            };
        }
        finally
        {
            // Decrease load after completion
            var currentLoad = await context.ReadStateAsync<int>($"load:{selectedAgent.Name}") ?? 0;
            await context.UpdateStateAsync($"load:{selectedAgent.Name}", Math.Max(0, currentLoad - 1));
        }
    }

    public async Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // Similar implementation with streaming support
        // ...
    }
}
```

### 2. Cost-Optimized Orchestrator

```csharp
public class CostOptimizedOrchestrator : IOrchestrator
{
    public async Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = context.GetAgents();
        var maxCost = request.GetExtension<decimal?>("maxCost") ?? decimal.MaxValue;
        var targetQuality = request.GetExtension<double>("targetQuality") ?? 0.8;
        
        // Calculate cost-quality ratio for each agent
        var agentScores = new Dictionary<string, float>();
        var eligibleAgents = new List<Agent>();
        
        foreach (var agent in agents)
        {
            var cost = GetAgentCost(agent);
            var quality = GetAgentQuality(agent);
            
            if (cost <= maxCost && quality >= targetQuality)
            {
                var score = (float)(quality / (double)cost); // Quality per dollar
                agentScores[agent.Name] = score;
                eligibleAgents.Add(agent);
            }
        }
        
        if (!eligibleAgents.Any())
        {
            throw new InvalidOperationException($"No agents meet cost constraint of {maxCost:C} and quality threshold of {targetQuality:P}");
        }
        
        // Select highest quality-per-cost agent
        var selectedAgent = eligibleAgents.OrderByDescending(a => agentScores[a.Name]).First();
        
        // Track spending
        var totalSpent = await context.ReadStateAsync<decimal>("totalSpent") ?? 0m;
        var agentCost = GetAgentCost(selectedAgent);
        await context.UpdateStateAsync("totalSpent", totalSpent + agentCost);
        
        // Emit cost tracking event
        await context.EmitEventAsync(new CostTrackingEvent(agentCost, totalSpent + agentCost, maxCost));
        
        var options = context.GetChatOptions();
        var streamingResult = await selectedAgent.ExecuteStreamingTurnAsync(
            request.GetChatHistory() ?? throw new InvalidOperationException("Chat history required"), 
            options, cancellationToken: cancellationToken);

        await foreach (var evt in streamingResult.EventStream.WithCancellation(cancellationToken))
        {
            await context.EmitEventAsync(evt);
        }

        var finalHistory = await streamingResult.FinalHistory;
        
        return new OrchestrationResult
        {
            Response = new ChatResponse(finalHistory),
            PrimaryAgent = selectedAgent,
            RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
            Status = OrchestrationStatus.Completed,
            Metadata = new OrchestrationMetadata
            {
                StrategyName = "CostOptimized",
                AgentScores = agentScores,
                Context = new Dictionary<string, object>
                {
                    ["selectedCost"] = agentCost,
                    ["totalSpent"] = totalSpent + agentCost,
                    ["qualityScore"] = GetAgentQuality(selectedAgent)
                }
            }
        };
    }

    private decimal GetAgentCost(Agent agent) => 
        agent.Config?.Extensions?.TryGetValue("cost", out var cost) == true ? (decimal)cost : 1.0m;
    
    private double GetAgentQuality(Agent agent) => 
        agent.Config?.Extensions?.TryGetValue("quality", out var quality) == true ? (double)quality : 0.8;

    public Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        // Implementation similar to above
        throw new NotImplementedException();
    }
}

// Custom event for cost tracking
public record CostTrackingEvent(decimal CurrentCost, decimal TotalSpent, decimal Budget) : BaseEvent;
```

## Comparison with Other Frameworks

| Feature | Microsoft Workflows | Pydantic Graph | **HPD-Agent** |
|---------|-------------------|----------------|---------------|
| **API Pattern** | `Handle(message, context)` | `run(node, state, deps)` | `Orchestrate(request, context)` |
| **Data/Service Separation** | ✅ Good | ✅ Good | ✅ **Excellent** |
| **Serialization** | ⚠️ Partial | ✅ Good | ✅ **Full** |
| **Type Safety** | ✅ C# | ⚠️ Python | ✅ **C# + Rich Types** |
| **State Management** | ✅ Scoped | ⚠️ Basic | ✅ **Scoped + Persistent** |
| **Extensibility** | ⚠️ Limited | ⚠️ Limited | ✅ **Unlimited** |
| **Built-in Events** | ⚠️ Basic | ❌ None | ✅ **Rich Event System** |
| **Checkpointing** | ❌ Manual | ✅ Built-in | ✅ **Integrated** |
| **Business Context** | ❌ None | ❌ None | ✅ **Priority, SLA, Cost** |
| **Testing Support** | ⚠️ Complex | ⚠️ Complex | ✅ **Excellent** |

### Key Advantages of HPD-Agent

1. **Superior Extensibility** - Extensions dictionary allows unlimited customization
2. **Rich Business Context** - Built-in support for priority, cost, SLA constraints
3. **Integrated Observability** - Events, tracing, and metrics built-in
4. **Enterprise State Management** - Scoped, persistent state with cleanup
5. **First-Class Serialization** - Full request/response serialization for any scenario
6. **Excellent Testing** - Clean separation enables simple mocking and testing

---

## Getting Started

To start using the HPD-Agent Orchestration Framework:

1. **Implement `IOrchestrator`** using the Request + Context pattern
2. **Use built-in state management** via `context.ReadStateAsync/UpdateStateAsync`
3. **Leverage extensions** for custom configuration via `request.Extensions`
4. **Emit events** for observability via `context.EmitEventAsync`
5. **Test thoroughly** using the improved mocking capabilities

The framework automatically handles the Request + Context creation when you use it with the `Conversation` class, providing a seamless developer experience while enabling powerful orchestration capabilities.