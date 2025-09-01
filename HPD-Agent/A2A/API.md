# A2A API Endpoints Documentation

## Overview

The HPD-Agent A2A integration exposes standard A2A protocol endpoints through the AgentAPI web server. These endpoints enable interoperability with other A2A-compatible agents and client applications.

## Base Configuration

- **Base URL**: `http://localhost:5135` (development)
- **A2A Endpoint**: `/a2a-agent`
- **Protocol Version**: A2A 0.2.6
- **Transport**: JSON-RPC 2.0

## Endpoints

### 1. JSON-RPC Message Endpoint

**Endpoint:** `POST /a2a-agent`
**Content-Type:** `application/json`
**Protocol:** JSON-RPC 2.0

This endpoint handles all A2A protocol operations via JSON-RPC method calls.

#### Supported Methods

##### message/send

Sends a message to the agent and returns a completed task with the response.

**Request Format:**
```json
{
    "id": "1",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "messageId": "msg-001",
            "role": "user",
            "parts": [
                {
                    "kind": "text",
                    "text": "What is 5 plus 5?"
                }
            ]
        }
    }
}
```

**Response Format:**
```json
{
    "jsonrpc": "2.0",
    "id": "1",
    "result": {
        "kind": "task",
        "id": "d2f16b24-890d-4fcb-a4dd-d109a4e68265",
        "contextId": "77b716ad-c65e-4d85-9696-a5c4ea64ac7c",
        "status": {
            "state": "completed",
            "timestamp": "2025-08-31T22:05:09.386341+00:00"
        },
        "artifacts": [
            {
                "artifactId": "086e2166-a454-487b-92cb-acf5ac507be2",
                "parts": [
                    {
                        "kind": "text",
                        "text": "5 plus 5 is 10."
                    }
                ]
            }
        ],
        "history": [
            {
                "role": "user",
                "parts": [
                    {
                        "kind": "text",
                        "text": "What is 5 plus 5?"
                    }
                ],
                "messageId": "msg-001"
            }
        ]
    }
}
```

##### message/stream

Streams real-time updates as the agent processes the message.

**Request Format:** Same as `message/send`

**Response:** Server-Sent Events (SSE) stream with task updates, messages, and artifacts

##### Other Methods

The endpoint also supports standard A2A task management methods:
- `task/get`: Retrieve task by ID
- `task/cancel`: Cancel a running task
- `task/subscribe`: Subscribe to task updates
- `agent/card`: Get agent capabilities (handled via JSON-RPC)

### 2. Agent Card Discovery

Agent capabilities are discoverable through the JSON-RPC interface using the `agent/card` method, or through direct agent card queries handled by the task manager.

**Agent Card Response Example:**
```json
{
    "name": "AI Assistant",
    "description": "You are a helpful AI assistant with memory, knowledge base, and web search capabilities.",
    "url": "http://localhost:5135/a2a-agent",
    "version": "1.0.0",
    "protocolVersion": "0.2.6",
    "capabilities": {
        "streaming": true,
        "pushNotifications": false
    },
    "skills": [
        {
            "id": "Add",
            "name": "Add",
            "description": "Adds two numbers",
            "tags": ["plugin-function"]
        },
        {
            "id": "Subtract",
            "name": "Subtract", 
            "description": "Subtracts two numbers",
            "tags": ["plugin-function"]
        }
    ],
    "defaultInputModes": ["text/plain"],
    "defaultOutputModes": ["text/plain"]
}
```

## Message Formats

### A2A Message Structure

```json
{
    "messageId": "unique-message-id",
    "role": "user|agent",
    "parts": [
        {
            "kind": "text",
            "text": "Message content"
        }
    ],
    "taskId": "optional-task-id",
    "contextId": "optional-context-id"
}
```

### Supported Part Types

Currently supported:
- **text**: Plain text content
- Future: **file**, **data**, **image**, **audio**

### Task States

- **submitted**: Task created, waiting to be processed
- **working**: Agent actively processing the task
- **completed**: Task finished successfully with artifacts
- **failed**: Task encountered an error
- **canceled**: Task was canceled by user/system

## Error Responses

### JSON-RPC Errors
```json
{
    "jsonrpc": "2.0",
    "id": "1",
    "error": {
        "code": -32000,
        "message": "Internal error description"
    }
}
```

### Task Failure
```json
{
    "result": {
        "kind": "task",
        "id": "task-id",
        "status": {
            "state": "failed",
            "timestamp": "2025-08-31T22:05:09Z"
        },
        "history": [
            {
                "role": "agent",
                "parts": [
                    {
                        "kind": "text",
                        "text": "Error message details"
                    }
                ]
            }
        ]
    }
}
```

## Integration Features

### Plugin Exposure
- All HPD-Agent plugins are automatically exposed as A2A skills
- Plugin functions appear in the agent card skills list
- Function descriptions and metadata are preserved
- Tags help categorize plugin capabilities

### Conversation Continuity
- Multi-turn conversations supported through task updates
- Conversation history maintained across requests
- Context preserved for complex, stateful interactions

### Streaming Support
- Real-time response streaming via SSE
- Progress updates during long-running tasks
- Incremental artifact delivery

## Security Considerations

- **Input Validation**: All A2A messages are validated
- **Error Sanitization**: Error messages don't expose sensitive information
- **Resource Limits**: Conversation memory and task limits apply
- **CORS**: Configured for appropriate origin restrictions

## Performance Notes

- **Conversation Caching**: Active conversations cached in memory
- **Concurrent Processing**: Thread-safe task handling
- **Resource Management**: Automatic cleanup of completed tasks
- **JSON Optimization**: Uses source-generated JSON serialization where possible