# A2AMapper Class Documentation

## Overview

The `A2AMapper` class provides static utility methods for converting data models between the A2A protocol and HPD-Agent's internal representation. This isolates the mapping logic and makes it easy to extend support for additional content types.

## Class Definition

```csharp
public static class A2AMapper
```

## Methods

### ToHpdChatMessage

```csharp
public static ChatMessage ToHpdChatMessage(Message a2aMessage)
```

**Purpose:** Converts an A2A Message to an HPD-Agent ChatMessage.

**Parameters:**
- `a2aMessage`: The A2A message to convert

**Returns:** A `ChatMessage` instance compatible with HPD-Agent conversations

**Implementation Details:**
- Extracts text content from A2A message parts
- Maps A2A message roles to HPD-Agent chat roles:
  - `MessageRole.User` → `ChatRole.User`
  - `MessageRole.Agent` → `ChatRole.Assistant`
- Currently supports `TextPart` content types
- Falls back to empty string if no text content found

**Example A2A Input:**
```json
{
  "role": "user",
  "messageId": "msg-001",
  "parts": [
    {
      "kind": "text",
      "text": "What is 5 plus 5?"
    }
  ]
}
```

**HPD-Agent Output:**
```csharp
new ChatMessage(ChatRole.User, "What is 5 plus 5?")
```

### ToA2AArtifact

```csharp
public static Artifact ToA2AArtifact(ChatResponse hpdResponse)
```

**Purpose:** Converts HPD-Agent ChatResponse into an A2A Artifact.

**Parameters:**
- `hpdResponse`: The chat response from HPD-Agent

**Returns:** An `Artifact` containing the agent's response formatted for A2A protocol

**Implementation Details:**
- Extracts text content from the last assistant message
- Generates unique artifact ID using `Guid.NewGuid()`
- Wraps response text in a `TextPart`
- Falls back to "No response." if no content found

**Example HPD-Agent Input:**
```csharp
ChatResponse with Messages: [
  new ChatMessage(ChatRole.Assistant, "5 plus 5 equals 10.")
]
```

**A2A Output:**
```json
{
  "artifactId": "086e2166-a454-487b-92cb-acf5ac507be2",
  "parts": [
    {
      "kind": "text",
      "text": "5 plus 5 equals 10."
    }
  ]
}
```

## Content Type Support

### Currently Supported
- **Text Content**: Full support for text-based messages and responses
- **Simple Messages**: Basic user queries and agent responses

### Future Extensions
The mapper can be extended to support:
- **File Parts**: Document and image attachments
- **Data Parts**: Structured data exchange
- **Media Content**: Audio/video content types
- **Function Calls**: Direct tool invocation mapping
- **Rich Formatting**: Markdown, HTML, or structured text

## Usage Patterns

### Basic Message Conversion
```csharp
// Convert incoming A2A message
var chatMessage = A2AMapper.ToHpdChatMessage(a2aMessage);
conversation.AddMessage(chatMessage);

// Convert outgoing response
var artifact = A2AMapper.ToA2AArtifact(response);
await taskManager.ReturnArtifactAsync(taskId, artifact);
```

### Error Handling
```csharp
// Create error artifact
var errorResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Error occurred"));
var errorArtifact = A2AMapper.ToA2AArtifact(errorResponse);
```

## Extension Guidelines

To add support for new content types:

1. **Check Content Type**: Inspect the A2A message parts for new types
2. **Map to HPD-Agent**: Convert to appropriate HPD-Agent content representation
3. **Handle Response**: Convert HPD-Agent response back to A2A format
4. **Test Thoroughly**: Ensure round-trip conversion preserves data integrity

### Example Extension for File Support

```csharp
public static ChatMessage ToHpdChatMessage(Message a2aMessage)
{
    var textContent = a2aMessage.Parts.OfType<TextPart>().FirstOrDefault()?.Text ?? string.Empty;
    var fileAttachments = a2aMessage.Parts.OfType<FilePart>().ToList();
    
    // Handle file attachments...
    
    var role = a2aMessage.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant;
    return new ChatMessage(role, textContent);
}
```

## Best Practices

1. **Null Safety**: Always check for null values and provide fallbacks
2. **Type Safety**: Use strongly typed conversions with proper casting
3. **Performance**: Minimize object allocation in conversion methods
4. **Extensibility**: Design for easy addition of new content types
5. **Testing**: Validate conversions with real A2A message examples