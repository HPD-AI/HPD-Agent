using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using HPD.Agent;
using HPD.Agent.Conversation;
using HPD_Agent.FFI;

namespace HPD_Agent.FFI;

/// <summary>
/// JSON serialization context for HPD-Agent FFI exports (AOT-compatible).
/// Includes all core types plus FFI-specific types like RustFunctionInfo, PluginRegistry, etc.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true, 
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
// --- Framework-specific types ---
[JsonSerializable(typeof(ValidationErrorResponse))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(List<ValidationError>))]

// --- Common primitive and collection types for AI function return values ---
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(IDictionary<string, object?>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<double>))]
[JsonSerializable(typeof(List<object>))]

// --- Schema library types for AOT compatibility ---
[JsonSerializable(typeof(Json.Schema.JsonSchema))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonNode))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonObject))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonArray))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonValue))]

// --- Agent configuration types ---
[JsonSerializable(typeof(AgentConfig))]
[JsonSerializable(typeof(ProviderConfig))]
[JsonSerializable(typeof(DynamicMemoryConfig))]
[JsonSerializable(typeof(McpConfig))]
[JsonSerializable(typeof(WebSearchConfig))]
[JsonSerializable(typeof(ValidationConfig))]
[JsonSerializable(typeof(ErrorHandlingConfig))]
[JsonSerializable(typeof(DocumentHandlingConfig))]
[JsonSerializable(typeof(HistoryReductionConfig))]
[JsonSerializable(typeof(StaticMemoryConfig))]

// --- Conversation and messaging types ---
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatRole))]
[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(ChatOptions))]

// --- Extensions.AI types for conversation support ---
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatRole))]
[JsonSerializable(typeof(ChatOptions))]
[JsonSerializable(typeof(UsageDetails))]
[JsonSerializable(typeof(AdditionalPropertiesDictionary))]
[JsonSerializable(typeof(ChatFinishReason))]
[JsonSerializable(typeof(ChatResponseUpdate))]
[JsonSerializable(typeof(FunctionCallContent))]
[JsonSerializable(typeof(FunctionResultContent))]
[JsonSerializable(typeof(AIContent))]
[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(List<AIContent>))]
[JsonSerializable(typeof(IList<ChatMessage>))]
[JsonSerializable(typeof(IEnumerable<ChatMessage>))]

// --- FFI-specific native plugin types (language-agnostic) ---
[JsonSerializable(typeof(NativeFunctionInfo))]
[JsonSerializable(typeof(List<NativeFunctionInfo>))]
[JsonSerializable(typeof(PluginRegistry))]
[JsonSerializable(typeof(PluginInfo))]
[JsonSerializable(typeof(FunctionInfo))]
[JsonSerializable(typeof(PluginStats))]
[JsonSerializable(typeof(PluginSummary))]
[JsonSerializable(typeof(PluginExecutionResult))]

// --- Internal Agent Event Types (for protocol adapters) ---
[JsonSerializable(typeof(InternalAgentEvent))]
[JsonSerializable(typeof(InternalMessageTurnStartedEvent))]
[JsonSerializable(typeof(InternalMessageTurnFinishedEvent))]
[JsonSerializable(typeof(InternalMessageTurnErrorEvent))]
[JsonSerializable(typeof(InternalAgentTurnStartedEvent))]
[JsonSerializable(typeof(InternalAgentTurnFinishedEvent))]
[JsonSerializable(typeof(InternalTextMessageStartEvent))]
[JsonSerializable(typeof(InternalTextDeltaEvent))]
[JsonSerializable(typeof(InternalTextMessageEndEvent))]
[JsonSerializable(typeof(InternalReasoningStartEvent))]
[JsonSerializable(typeof(InternalReasoningMessageStartEvent))]
[JsonSerializable(typeof(InternalReasoningDeltaEvent))]
[JsonSerializable(typeof(InternalReasoningMessageEndEvent))]
[JsonSerializable(typeof(InternalReasoningEndEvent))]
[JsonSerializable(typeof(InternalToolCallStartEvent))]
[JsonSerializable(typeof(InternalToolCallArgsEvent))]
[JsonSerializable(typeof(InternalToolCallEndEvent))]
[JsonSerializable(typeof(InternalToolCallResultEvent))]
[JsonSerializable(typeof(InternalPermissionRequestEvent))]
[JsonSerializable(typeof(InternalPermissionResponseEvent))]
[JsonSerializable(typeof(InternalPermissionApprovedEvent))]
[JsonSerializable(typeof(InternalPermissionDeniedEvent))]
[JsonSerializable(typeof(InternalContinuationRequestEvent))]
[JsonSerializable(typeof(InternalContinuationResponseEvent))]
[JsonSerializable(typeof(InternalClarificationRequestEvent))]
[JsonSerializable(typeof(InternalClarificationResponseEvent))]
[JsonSerializable(typeof(InternalFilterProgressEvent))]
[JsonSerializable(typeof(InternalFilterErrorEvent))]

// --- Agent State Types ---
[JsonSerializable(typeof(AgentLoopState))]

// --- Checkpointing / Resume Types ---
[JsonSerializable(typeof(ConversationThreadSnapshot))]
[JsonSerializable(typeof(HistoryReductionState))]

// --- Permission Types ---
[JsonSerializable(typeof(PermissionChoice))]
[JsonSerializable(typeof(PermissionScope))]
[JsonSerializable(typeof(PermissionDecision))]
[JsonSerializable(typeof(PermissionStorage))]

public partial class HPDFFIJsonContext : JsonSerializerContext
{
}
