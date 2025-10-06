using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using A2A;
using HPD;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
)]
// ✨ SIMPLIFIED: Application-specific types only
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(AgentChatResponse))]
[JsonSerializable(typeof(UsageInfo))]
[JsonSerializable(typeof(StreamRequest))]
[JsonSerializable(typeof(StreamMessage[]))]
[JsonSerializable(typeof(List<StreamMessage>))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(SttResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(IEnumerable<ProjectDto>))]
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(IEnumerable<ConversationDto>))]
[JsonSerializable(typeof(ConversationWithMessagesDto))]
[JsonSerializable(typeof(ConversationMessageDto))]
[JsonSerializable(typeof(ConversationMessageDto[]))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(CreateConversationRequest))]
[JsonSerializable(typeof(ProjectDto[]))]
[JsonSerializable(typeof(ConversationDto[]))]

// Streaming response types for AOT compatibility
[JsonSerializable(typeof(StreamContentResponse))]
[JsonSerializable(typeof(StreamFinishResponse))]
[JsonSerializable(typeof(StreamErrorResponse))]
[JsonSerializable(typeof(StreamMetadataResponse))]
[JsonSerializable(typeof(ContentEvent))]
[JsonSerializable(typeof(FinishEvent))]

// AG-UI Protocol types (already in AGUIJsonContext, but explicit reference for API)
[JsonSerializable(typeof(RunAgentInput))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Provides combined JSON type info resolvers for the application.
/// Uses the library's AGUIJsonSerializerHelper for consistent configuration.
/// </summary>
internal static class JsonResolvers
{
    /// <summary>
    /// Combined type info resolver that includes App, HPD, AGUI, and A2A types.
    /// App types are first in the chain to ensure API DTOs are found first.
    /// </summary>
    public static IJsonTypeInfoResolver Combined { get; } =
        AGUIJsonSerializerHelper.CreateCombinedResolver(AppJsonSerializerContext.Default);
}

// Streaming response types for AOT compatibility
public record StreamContentResponse(string content);
public record StreamFinishResponse(bool finished, string reason);
public record StreamErrorResponse(string error);
public record StreamMetadataResponse(long TotalTokens, double DurationSeconds, string AgentName, decimal? EstimatedCost);

// Event types for frontend compatibility
public record ContentEvent(string type, string content);
public record FinishEvent(string type, string reason);