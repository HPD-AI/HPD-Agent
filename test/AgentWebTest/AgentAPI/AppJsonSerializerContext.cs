using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

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
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(IEnumerable<ConversationDto>))]
[JsonSerializable(typeof(ConversationWithMessagesDto))]
[JsonSerializable(typeof(ConversationMessageDto))]
[JsonSerializable(typeof(ConversationMessageDto[]))]
[JsonSerializable(typeof(CreateConversationRequest))]
[JsonSerializable(typeof(ConversationDto[]))]

// Streaming response types for AOT compatibility
[JsonSerializable(typeof(StreamContentResponse))]
[JsonSerializable(typeof(StreamFinishResponse))]
[JsonSerializable(typeof(StreamErrorResponse))]
[JsonSerializable(typeof(StreamMetadataResponse))]
[JsonSerializable(typeof(ContentEvent))]
[JsonSerializable(typeof(FinishEvent))]

internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Provides combined JSON type info resolvers for the application.
/// </summary>
internal static class JsonResolvers
{
    /// <summary>
    /// Combined type info resolver that includes App types.
    /// </summary>
    public static IJsonTypeInfoResolver Combined { get; } = AppJsonSerializerContext.Default;
}

// Streaming response types for AOT compatibility
public record StreamContentResponse(string content);
public record StreamFinishResponse(bool finished, string reason);
public record StreamErrorResponse(string error);
public record StreamMetadataResponse(long TotalTokens, double DurationSeconds, string AgentName, decimal? EstimatedCost);

// Event types for frontend compatibility
public record ContentEvent(string type, string content);
public record FinishEvent(string type, string reason);