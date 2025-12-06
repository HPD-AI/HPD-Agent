using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
// App-specific DTOs only - FrontendTools types come from HPDJsonContext in the library
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(ConversationDto[]))]
[JsonSerializable(typeof(List<ConversationDto>))]
[JsonSerializable(typeof(StreamRequest))]
[JsonSerializable(typeof(StreamMessage))]
[JsonSerializable(typeof(StreamMessage[]))]
[JsonSerializable(typeof(PermissionResponseRequest))]
[JsonSerializable(typeof(SuccessResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(FrontendToolResponseRequest))]
[JsonSerializable(typeof(FrontendToolContentDto))]
[JsonSerializable(typeof(FrontendToolContentDto[]))]
// Branch DTOs
[JsonSerializable(typeof(BranchDto))]
[JsonSerializable(typeof(List<BranchDto>))]
[JsonSerializable(typeof(BranchTreeDto))]
[JsonSerializable(typeof(BranchNodeDto))]
[JsonSerializable(typeof(BranchMetadataDto))]
[JsonSerializable(typeof(ForkRequest))]
[JsonSerializable(typeof(RenameBranchRequest))]
[JsonSerializable(typeof(BranchCreatedDto))]
[JsonSerializable(typeof(BranchSwitchedDto))]
[JsonSerializable(typeof(BranchDeletedDto))]
[JsonSerializable(typeof(BranchRenamedDto))]
[JsonSerializable(typeof(CheckpointDto))]
[JsonSerializable(typeof(List<CheckpointDto>))]
[JsonSerializable(typeof(VariantDto))]
[JsonSerializable(typeof(List<VariantDto>))]
[JsonSerializable(typeof(MessageDto))]
[JsonSerializable(typeof(List<MessageDto>))]
[JsonSerializable(typeof(Dictionary<string, BranchNodeDto>))]
[JsonSerializable(typeof(Dictionary<string, BranchMetadataDto>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
