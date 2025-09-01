# A2AHandler Class Documentation

## Overview

The `A2AHandler` class serves as the central adapter between the A2A protocol and HPD-Agent's conversation system. It implements the event-driven pattern required by the A2A framework, handling task lifecycle events and maintaining conversation state.

## Class Definition

```csharp
public class A2AHandler
```

## Constructor

```csharp
public A2AHandler(Agent agent, ITaskManager taskManager)
```

**Parameters:**
- `agent`: The HPD-Agent instance to use for processing messages
- `taskManager`: The A2A task manager that handles protocol events

**Functionality:**
- Stores references to the agent and task manager
- Registers event handlers for A2A task lifecycle events
- Initializes the conversation tracking dictionary

## Fields

### Private Fields

- `_agent`: The HPD-Agent instance used for message processing
- `_taskManager`: The A2A task manager for protocol communication
- `_activeConversations`: Thread-safe dictionary mapping A2A task IDs to HPD-Agent conversations

## Methods

### GetAgentCardAsync

```csharp
private Task<AgentCard> GetAgentCardAsync(string agentUrl, CancellationToken cancellationToken)
```

**Purpose:** Generates an A2A agent card that describes the HPD-Agent's capabilities.

**Parameters:**
- `agentUrl`: The URL where this agent is accessible
- `cancellationToken`: Cancellation token for the async operation

**Returns:** An `AgentCard` containing:
- Agent name and description
- Available skills (derived from agent plugins)
- Supported capabilities (streaming, etc.)
- Input/output modes
- Protocol version information

**Implementation Details:**
- Dynamically inspects agent's configured tools/plugins
- Converts each `AIFunction` to an `AgentSkill`
- Sets capabilities based on agent configuration
- Returns protocol version 0.2.6 compliance

### ProcessAndRespondAsync

```csharp
private async Task ProcessAndRespondAsync(AgentTask task, Conversation conversation, Message a2aMessage, CancellationToken cancellationToken)
```

**Purpose:** Core processing logic shared between task creation and updates.

**Parameters:**
- `task`: The A2A task being processed
- `conversation`: The HPD-Agent conversation instance
- `a2aMessage`: The A2A message to process
- `cancellationToken`: Cancellation token

**Workflow:**
1. Updates A2A task status to "working"
2. Converts A2A message to HPD-Agent format using `A2AMapper`
3. Adds message to conversation history
4. Processes the complete conversation through the agent
5. Converts agent response to A2A artifact
6. Returns artifact to A2A task
7. Marks task as "completed"

**Error Handling:**
- Catches all exceptions during processing
- Creates error message in A2A format
- Updates task status to "failed" with error details
- Ensures proper task finalization

### OnTaskCreatedAsync

```csharp
private async Task OnTaskCreatedAsync(AgentTask task, CancellationToken cancellationToken)
```

**Purpose:** Handles new A2A task creation events.

**Parameters:**
- `task`: The newly created A2A task
- `cancellationToken`: Cancellation token

**Functionality:**
- Creates a new HPD-Agent conversation for the task
- Stores the conversation in the active conversations dictionary
- Processes the initial message if present in task history
- Initiates the conversation flow

### OnTaskUpdatedAsync

```csharp
private async Task OnTaskUpdatedAsync(AgentTask task, CancellationToken cancellationToken)
```

**Purpose:** Handles A2A task update events for multi-turn conversations.

**Parameters:**
- `task`: The updated A2A task
- `cancellationToken`: Cancellation token

**Functionality:**
- Retrieves existing conversation for the task ID
- If conversation exists: processes the new message in context
- If conversation missing (e.g., server restart):
  - Creates new conversation
  - Rebuilds conversation history from A2A task history
  - Processes the latest message
- Maintains conversation continuity across task updates

## Conversation Management

### State Tracking
- Maps A2A task IDs to HPD-Agent conversations using `ConcurrentDictionary`
- Maintains conversation state across multiple task updates
- Handles server restart scenarios by rebuilding from task history

### Thread Safety
- Uses `ConcurrentDictionary` for thread-safe conversation access
- Supports concurrent task processing
- Ensures conversation integrity across parallel requests

## Integration Points

### With HPD-Agent
- Uses `Agent.GetResponseAsync()` for message processing
- Leverages full HPD-Agent capability stack (plugins, memory, etc.)
- Maintains conversation context and history

### With A2A Protocol
- Implements required `ITaskManager` event handlers
- Follows A2A task state transition patterns
- Provides proper artifact and error reporting
- Supports A2A streaming and push notification interfaces

## Error Handling Strategy

1. **Graceful Degradation**: Errors don't crash the service
2. **Proper Reporting**: A2A tasks receive detailed error information
3. **State Management**: Failed tasks are properly marked and finalized
4. **Logging**: Errors are captured for debugging and monitoring
5. **Recovery**: Missing conversations can be rebuilt from task history