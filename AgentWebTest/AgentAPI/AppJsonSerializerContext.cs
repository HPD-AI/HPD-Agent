using System.Text.Json.Serialization;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ConversationRequest))]
[JsonSerializable(typeof(ConversationMessage[]))]
[JsonSerializable(typeof(AgentChatResponse))]
[JsonSerializable(typeof(UsageInfo))]
[JsonSerializable(typeof(StreamRequest))]
[JsonSerializable(typeof(StreamMessage[]))]
[JsonSerializable(typeof(ModelsResponse))]
[JsonSerializable(typeof(ModelInfo[]))]
// Event types for AGUI streaming
[JsonSerializable(typeof(BaseEvent))]
[JsonSerializable(typeof(RunStartedEvent))]
[JsonSerializable(typeof(RunFinishedEvent))]
[JsonSerializable(typeof(RunErrorEvent))]
[JsonSerializable(typeof(TextMessageStartEvent))]
[JsonSerializable(typeof(TextMessageContentEvent))]
[JsonSerializable(typeof(TextMessageEndEvent))]
[JsonSerializable(typeof(ToolCallStartEvent))]
[JsonSerializable(typeof(ToolCallArgsEvent))]
[JsonSerializable(typeof(ToolCallEndEvent))]
[JsonSerializable(typeof(StepStartedEvent))]
[JsonSerializable(typeof(StepFinishedEvent))]
[JsonSerializable(typeof(StateDeltaEvent))]
[JsonSerializable(typeof(StateSnapshotEvent))]
[JsonSerializable(typeof(MessagesSnapshotEvent))]
[JsonSerializable(typeof(CustomEvent))]
[JsonSerializable(typeof(RawEvent))]
// FIX: Added missing dictionary types for AOT compatibility
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
[JsonSerializable(typeof(List<ConversationMessage>))]
[JsonSerializable(typeof(List<StreamMessage>))]
// Add ProblemDetails for error handling
[JsonSerializable(typeof(ProblemDetails))]
// Add STT response types
[JsonSerializable(typeof(SttResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
// New DTOs and requests
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(IEnumerable<ProjectDto>))]
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(ConversationWithMessagesDto))]
[JsonSerializable(typeof(IEnumerable<ConversationDto>))]
[JsonSerializable(typeof(MessageDto))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(CreateConversationRequest))]
[JsonSerializable(typeof(UpdateProjectRequest))]
[JsonSerializable(typeof(ProjectDto[]))]
[JsonSerializable(typeof(ConversationDto[]))]
[JsonSerializable(typeof(MessageDto[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}