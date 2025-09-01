# HPD-Agent A2A Protocol Integration

## Overview

This integration adds support for the [A2A (Agent-to-Agent) protocol](https://github.com/a2aproject/A2A) to HPD-Agent, enabling interoperability with other A2A-compatible AI agents and systems. The A2A protocol provides standardized APIs for task management, message exchange, and capability discovery between autonomous agents.

## Architecture

The A2A integration consists of three main components:

### 1. A2AHandler (`A2AHandler.cs`)
The central adapter that bridges the A2A protocol with HPD-Agent's internal conversation system. It:
- Manages the lifecycle of A2A tasks
- Converts between A2A and HPD-Agent data models
- Handles task creation, updates, and completion
- Maintains conversation state for multi-turn interactions

### 2. A2AMapper (`A2AMapper.cs`)
Static utility class responsible for data model conversion:
- Converts A2A Messages to HPD-Agent ChatMessages
- Transforms HPD-Agent ChatResponses into A2A Artifacts
- Handles role mapping between the two systems

### 3. AgentAPI Integration (`Program.cs`)
The web API exposes A2A endpoints alongside existing AGUI endpoints:
- JSON-RPC endpoint for A2A protocol communication
- Agent capability discovery
- Task management and streaming support

## Key Features

- **Protocol Compliance**: Full A2A 0.2.6 protocol support
- **Plugin Exposure**: HPD-Agent plugins (e.g., MathPlugin) are automatically exposed as A2A skills
- **Multi-turn Conversations**: Maintains conversation context across multiple A2A task updates
- **Error Handling**: Proper A2A task state management for failures and exceptions
- **Streaming Support**: Built-in support for A2A streaming protocols
- **Agent Discovery**: Automatic capability advertisement through agent cards

## Data Flow

```
A2A Client → JSON-RPC Request → A2AHandler → Conversation → Agent → Response
                ↓
A2A Task Creation → Message Mapping → HPD-Agent Processing → Artifact Return
```

1. **Incoming A2A Message**: Received via JSON-RPC at `/a2a-agent`
2. **Task Management**: A2A task created with "working" status
3. **Message Conversion**: A2A Message converted to HPD-Agent ChatMessage
4. **Agent Processing**: HPD-Agent processes the message using its full capability stack
5. **Response Mapping**: ChatResponse converted to A2A Artifact
6. **Task Completion**: A2A task marked as "completed" with artifact attached

## Configuration

The A2A integration is automatically configured in `Program.cs`:

```csharp
// A2A Integration Setup
var taskManager = new TaskManager(taskStore: new InMemoryTaskStore());
var a2aHandler = new A2AHandler(agent, taskManager);
app.MapA2A(taskManager, "/a2a-agent");
```

## Supported Capabilities

The agent card dynamically exposes capabilities based on the HPD-Agent configuration:

- **Text Processing**: All text-based interactions
- **Function Calling**: Agent plugins exposed as A2A skills
- **Memory Management**: Conversation persistence across task updates
- **Streaming**: Real-time response streaming
- **Error Recovery**: Graceful handling of failures with proper A2A status reporting

## Dependencies

- `A2A.AspNetCore` (0.1.0-preview.2): Core A2A protocol implementation
- `Microsoft.Extensions.AI`: HPD-Agent's AI framework
- `System.Text.Json`: JSON serialization for A2A data models

## Integration Benefits

1. **Interoperability**: HPD-Agent can now participate in multi-agent workflows
2. **Standardization**: Uses industry-standard A2A protocol for agent communication
3. **Scalability**: Supports both single-shot and long-running collaborative tasks
4. **Discoverability**: Automatic capability advertisement for agent marketplaces
5. **Flexibility**: Maintains full HPD-Agent functionality while adding A2A support