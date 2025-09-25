using System.Text.Json;

/// <summary>
/// Internal utility for AG-UI event serialization and creation.
/// This avoids circular dependencies between Agent and AGUIEventConverter.
/// </summary>
internal static class EventSerialization
{
    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Serializes an AG-UI event with proper polymorphic serialization.
    /// Uses type switching to ensure all derived properties are included.
    /// </summary>
    /// <param name="evt">The AG-UI event to serialize</param>
    /// <returns>JSON string with all event properties</returns>
    public static string SerializeEvent(BaseEvent evt)
    {
        return evt switch
        {
            TextMessageContentEvent textEvent => JsonSerializer.Serialize(textEvent, AGUIJsonContext.Default.TextMessageContentEvent),
            TextMessageStartEvent startEvent => JsonSerializer.Serialize(startEvent, AGUIJsonContext.Default.TextMessageStartEvent),
            TextMessageEndEvent endEvent => JsonSerializer.Serialize(endEvent, AGUIJsonContext.Default.TextMessageEndEvent),
            ToolCallStartEvent toolStartEvent => JsonSerializer.Serialize(toolStartEvent, AGUIJsonContext.Default.ToolCallStartEvent),
            ToolCallArgsEvent toolArgsEvent => JsonSerializer.Serialize(toolArgsEvent, AGUIJsonContext.Default.ToolCallArgsEvent),
            ToolCallEndEvent toolEndEvent => JsonSerializer.Serialize(toolEndEvent, AGUIJsonContext.Default.ToolCallEndEvent),
            RunStartedEvent runStartEvent => JsonSerializer.Serialize(runStartEvent, AGUIJsonContext.Default.RunStartedEvent),
            RunFinishedEvent runFinishEvent => JsonSerializer.Serialize(runFinishEvent, AGUIJsonContext.Default.RunFinishedEvent),
            RunErrorEvent runErrorEvent => JsonSerializer.Serialize(runErrorEvent, AGUIJsonContext.Default.RunErrorEvent),
            StepStartedEvent stepStartEvent => JsonSerializer.Serialize(stepStartEvent, AGUIJsonContext.Default.StepStartedEvent),
            StepFinishedEvent stepFinishEvent => JsonSerializer.Serialize(stepFinishEvent, AGUIJsonContext.Default.StepFinishedEvent),
            StateDeltaEvent stateDeltaEvent => JsonSerializer.Serialize(stateDeltaEvent, AGUIJsonContext.Default.StateDeltaEvent),
            StateSnapshotEvent stateSnapshotEvent => JsonSerializer.Serialize(stateSnapshotEvent, AGUIJsonContext.Default.StateSnapshotEvent),
            MessagesSnapshotEvent messagesSnapshotEvent => JsonSerializer.Serialize(messagesSnapshotEvent, AGUIJsonContext.Default.MessagesSnapshotEvent),
            OrchestrationStartEvent orchestrationStartEvent => JsonSerializer.Serialize(orchestrationStartEvent, AGUIJsonContext.Default.OrchestrationStartEvent),
            OrchestrationDecisionEvent orchestrationDecisionEvent => JsonSerializer.Serialize(orchestrationDecisionEvent, AGUIJsonContext.Default.OrchestrationDecisionEvent),
            AgentEvaluationEvent agentEvaluationEvent => JsonSerializer.Serialize(agentEvaluationEvent, AGUIJsonContext.Default.AgentEvaluationEvent),
            AgentHandoffEvent agentHandoffEvent => JsonSerializer.Serialize(agentHandoffEvent, AGUIJsonContext.Default.AgentHandoffEvent),
            OrchestrationCompleteEvent orchestrationCompleteEvent => JsonSerializer.Serialize(orchestrationCompleteEvent, AGUIJsonContext.Default.OrchestrationCompleteEvent),
            CustomEvent customEvent => JsonSerializer.Serialize(customEvent, AGUIJsonContext.Default.CustomEvent),
            RawEvent rawEvent => JsonSerializer.Serialize(rawEvent, AGUIJsonContext.Default.RawEvent),
            _ => JsonSerializer.Serialize(evt, AGUIJsonContext.Default.BaseEvent)
        };
    }

    // Event factory methods for internal use by AGUIEventConverter
    public static TextMessageContentEvent CreateTextMessageContent(string messageId, string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            throw new ArgumentException("Delta content cannot be null or empty", nameof(delta));
        }
        
        return new()
        {
            Type = "TEXT_MESSAGE_CONTENT",
            MessageId = messageId,
            Delta = delta,
            Timestamp = GetTimestamp()
        };
    }

    public static TextMessageStartEvent CreateTextMessageStart(string messageId, string? role = null) => new()
    {
        Type = "TEXT_MESSAGE_START",
        MessageId = messageId,
        Role = role,
        Timestamp = GetTimestamp()
    };

    public static TextMessageEndEvent CreateTextMessageEnd(string messageId) => new()
    {
        Type = "TEXT_MESSAGE_END",
        MessageId = messageId,
        Timestamp = GetTimestamp()
    };

    public static ToolCallStartEvent CreateToolCallStart(string toolCallId, string toolCallName, string parentMessageId) => new()
    {
        Type = "TOOL_CALL_START",
        ToolCallId = toolCallId,
        ToolCallName = toolCallName,
        ParentMessageId = parentMessageId,
        Timestamp = GetTimestamp()
    };

    public static ToolCallArgsEvent CreateToolCallArgs(string toolCallId, string delta) => new()
    {
        Type = "TOOL_CALL_ARGS",
        ToolCallId = toolCallId,
        Delta = delta,
        Timestamp = GetTimestamp()
    };

    public static ToolCallEndEvent CreateToolCallEnd(string toolCallId) => new()
    {
        Type = "TOOL_CALL_END",
        ToolCallId = toolCallId,
        Timestamp = GetTimestamp()
    };

    public static RunStartedEvent CreateRunStarted(string threadId, string runId) => new()
    {
        Type = "RUN_STARTED",
        ThreadId = threadId,
        RunId = runId,
        Timestamp = GetTimestamp()
    };

    public static RunFinishedEvent CreateRunFinished(string threadId, string runId) => new()
    {
        Type = "RUN_FINISHED",
        ThreadId = threadId,
        RunId = runId,
        Timestamp = GetTimestamp()
    };

    public static RunErrorEvent CreateRunError(string message) => new()
    {
        Type = "RUN_ERROR",
        Message = message,
        Timestamp = GetTimestamp()
    };

    public static CustomEvent CreateToolResult(string messageId, string toolCallId, string toolName, object result) => new()
    {
        Type = "CUSTOM",
        Data = JsonSerializer.SerializeToElement(new ToolResultEventData
        {
            EventType = "tool_result",
            MessageId = messageId,
            ToolCallId = toolCallId,
            ToolName = toolName,
            // FIX: For AOT compatibility, fall back to string representation for non-string results
            Result = result?.ToString() ?? "null"
        }, AGUIJsonContext.Default.ToolResultEventData),
        Timestamp = GetTimestamp()
    };

    public static CustomEvent CreateReasoningContent(string messageId, string content) => new()
    {
        Type = "CUSTOM",
        Data = JsonSerializer.SerializeToElement(new ReasoningContentEventData
        {
            EventType = "reasoning_content",
            MessageId = messageId,
            Content = content
        }, AGUIJsonContext.Default.ReasoningContentEventData),
        Timestamp = GetTimestamp()
    };

    // Standard event factory methods for consistency - only adding missing ones
    public static StepStartedEvent CreateStepStarted(string stepId, string stepName, string? description = null) => new()
    {
        Type = "STEP_STARTED",
        StepId = stepId,
        StepName = stepName,
        Description = description,
        Timestamp = GetTimestamp()
    };

    public static StepFinishedEvent CreateStepFinished(string stepId, string stepName, JsonElement? result = null) => new()
    {
        Type = "STEP_FINISHED",
        StepId = stepId,
        StepName = stepName,
        Result = result,
        Timestamp = GetTimestamp()
    };
}
