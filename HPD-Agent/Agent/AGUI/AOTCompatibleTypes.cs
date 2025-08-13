using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

/// <summary>
/// AOT-compatible AGUI types - copied and optimized for Native AOT
/// </summary>

// Input Types
public sealed record RunAgentInput
{
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("state")]
    public required JsonElement State { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<BaseMessage> Messages { get; init; } = [];

    [JsonPropertyName("tools")]
    public required IReadOnlyList<Tool> Tools { get; init; } = [];

    [JsonPropertyName("context")]
    public required IReadOnlyList<Context> Context { get; init; } = [];

    [JsonPropertyName("forwardedProps")]
    public required JsonElement ForwardedProps { get; init; }
}

// Message Types (simplified for AOT)
public abstract record BaseMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

public sealed record UserMessage : BaseMessage;
public sealed record AssistantMessage : BaseMessage;
public sealed record SystemMessage : BaseMessage;
public sealed record DeveloperMessage : BaseMessage;
public sealed record ToolMessage : BaseMessage;

// Event Types (with JsonSourceGenerator support)
public abstract record BaseEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}

public sealed record RunStartedEvent : BaseEvent
{
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }
}

public sealed record RunFinishedEvent : BaseEvent
{
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }
}

public sealed record TextMessageContentEvent : BaseEvent
{
    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }
    
    [JsonPropertyName("delta")]
    public required string Delta { get; init; }
}

public sealed record TextMessageStartEvent : BaseEvent
{
    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }
}

public sealed record TextMessageEndEvent : BaseEvent
{
    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }
}

public sealed record RunErrorEvent : BaseEvent
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record ToolCallStartEvent : BaseEvent
{
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }
    
    [JsonPropertyName("toolCallName")]
    public required string ToolCallName { get; init; }
    
    [JsonPropertyName("parentMessageId")]
    public required string ParentMessageId { get; init; }
}

public sealed record ToolCallArgsEvent : BaseEvent
{
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }
    
    [JsonPropertyName("delta")]
    public required string Delta { get; init; }
}

public sealed record ToolCallEndEvent : BaseEvent
{
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }
}

public sealed record CustomEvent : BaseEvent
{
    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }
}

public sealed record MessagesSnapshotEvent : BaseEvent
{
    [JsonPropertyName("messages")]
    public required IReadOnlyList<BaseMessage> Messages { get; init; }
}

public sealed record RawEvent : BaseEvent
{
    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }
}

public sealed record StateDeltaEvent : BaseEvent
{
    [JsonPropertyName("delta")]
    public required JsonElement Delta { get; init; }
}

public sealed record StateSnapshotEvent : BaseEvent
{
    [JsonPropertyName("state")]
    public required JsonElement State { get; init; }
}

public sealed record StepFinishedEvent : BaseEvent
{
    [JsonPropertyName("stepId")]
    public required string StepId { get; init; }
    
    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }
}

public sealed record StepStartedEvent : BaseEvent
{
    [JsonPropertyName("stepId")]
    public required string StepId { get; init; }
    
    [JsonPropertyName("stepName")]
    public required string StepName { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

// Tool Types
public sealed record Tool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("parameters")]
    public required JsonElement Parameters { get; init; }
}

public sealed record Context
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }
}

// AOT-compatible interface
public interface IAGUIAgent
{
    Task RunAsync(RunAgentInput input, ChannelWriter<BaseEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Helper methods for creating AGUI events with proper timestamps and type identifiers
/// </summary>
public static class EventHelpers
{
    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static RunStartedEvent CreateRunStarted(string threadId, string runId) => new()
    {
        Type = "run_started",
        ThreadId = threadId,
        RunId = runId,
        Timestamp = GetTimestamp()
    };

    public static RunFinishedEvent CreateRunFinished(string threadId, string runId) => new()
    {
        Type = "run_finished",
        ThreadId = threadId,
        RunId = runId,
        Timestamp = GetTimestamp()
    };

    public static RunErrorEvent CreateRunError(string message) => new()
    {
        Type = "run_error",
        Message = message,
        Timestamp = GetTimestamp()
    };

    public static TextMessageStartEvent CreateTextMessageStart(string messageId) => new()
    {
        Type = "text_message_start",
        MessageId = messageId,
        Timestamp = GetTimestamp()
    };

    public static TextMessageContentEvent CreateTextMessageContent(string messageId, string delta)
    {
        // Only create events with actual content
        if (string.IsNullOrEmpty(delta))
        {
            throw new ArgumentException("Delta content cannot be null or empty", nameof(delta));
        }
        
        return new()
        {
            Type = "text_message_content",
            MessageId = messageId,
            Delta = delta,
            Timestamp = GetTimestamp()
        };
    }

    public static TextMessageEndEvent CreateTextMessageEnd(string messageId) => new()
    {
        Type = "text_message_end",
        MessageId = messageId,
        Timestamp = GetTimestamp()
    };

    public static ToolCallStartEvent CreateToolCallStart(string toolCallId, string toolCallName, string parentMessageId) => new()
    {
        Type = "tool_call_start",
        ToolCallId = toolCallId,
        ToolCallName = toolCallName,
        ParentMessageId = parentMessageId,
        Timestamp = GetTimestamp()
    };

    public static ToolCallArgsEvent CreateToolCallArgs(string toolCallId, string delta) => new()
    {
        Type = "tool_call_args",
        ToolCallId = toolCallId,
        Delta = delta,
        Timestamp = GetTimestamp()
    };

    public static ToolCallEndEvent CreateToolCallEnd(string toolCallId) => new()
    {
        Type = "tool_call_end",
        ToolCallId = toolCallId,
        Timestamp = GetTimestamp()
    };

    public static StepStartedEvent CreateStepStarted(string stepId, string stepName, string? description = null) => new()
    {
        Type = "step_started",
        StepId = stepId,
        StepName = stepName,
        Description = description,
        Timestamp = GetTimestamp()
    };

    public static StepFinishedEvent CreateStepFinished(string stepId, JsonElement? result = null) => new()
    {
        Type = "step_finished",
        StepId = stepId,
        Result = result,
        Timestamp = GetTimestamp()
    };

    public static StateDeltaEvent CreateStateDelta(JsonElement delta) => new()
    {
        Type = "state_delta",
        Delta = delta,
        Timestamp = GetTimestamp()
    };

    public static StateSnapshotEvent CreateStateSnapshot(JsonElement state) => new()
    {
        Type = "state_snapshot",
        State = state,
        Timestamp = GetTimestamp()
    };

    public static MessagesSnapshotEvent CreateMessagesSnapshot(IReadOnlyList<BaseMessage> messages) => new()
    {
        Type = "messages_snapshot",
        Messages = messages,
        Timestamp = GetTimestamp()
    };

    public static CustomEvent CreateCustom(JsonElement data) => new()
    {
        Type = "custom",
        Data = data,
        Timestamp = GetTimestamp()
    };

    public static RawEvent CreateRaw(JsonElement data) => new()
    {
        Type = "raw",
        Data = data,
        Timestamp = GetTimestamp()
    };
}
