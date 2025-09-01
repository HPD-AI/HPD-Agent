# A2A Integration Testing Guide

## Overview

This guide provides comprehensive testing instructions for the HPD-Agent A2A protocol integration, including examples, troubleshooting, and verification procedures.

## Prerequisites

1. **Server Running**: AgentAPI server must be running on `http://localhost:5135`
2. **Configuration**: Ensure `appsettings.json` has valid API keys for providers
3. **Dependencies**: A2A.AspNetCore package installed and referenced

## Test Categories

### 1. Basic Connectivity Tests

#### Test Agent Availability
```bash
# Check if server is responding
curl -I http://localhost:5135/a2a-agent
# Expected: HTTP/1.1 405 Method Not Allowed (POST required)
```

#### Test JSON-RPC Format
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{"id":"test","jsonrpc":"2.0","method":"agent/info"}'
```

### 2. Agent Card Testing

Test agent capability discovery through the JSON-RPC interface:

```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "card-test",
    "jsonrpc": "2.0", 
    "method": "agent/card",
    "params": {}
  }'
```

**Expected Response:**
- Agent name: "AI Assistant"
- Skills array containing MathPlugin functions (Add, Subtract, Multiply, Divide)
- Streaming capability: true
- Protocol version: "0.2.6"

### 3. Message Processing Tests

#### Simple Text Message
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "1",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "messageId": "test-001",
            "role": "user",
            "parts": [
                {
                    "kind": "text",
                    "text": "Hello, how are you?"
                }
            ]
        }
    }
  }'
```

**Expected Response:**
- Status: "completed"
- Artifact with friendly response
- Message in task history

#### Math Plugin Function Test
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "2",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "messageId": "math-001",
            "role": "user",
            "parts": [
                {
                    "kind": "text",
                    "text": "What is 25 multiplied by 4?"
                }
            ]
        }
    }
  }'
```

**Expected Response:**
- Artifact containing "100" or "25 multiplied by 4 is 100"
- Evidence of Multiply function usage in response

### 4. Multi-turn Conversation Tests

#### Continuing a Task
```bash
# First message
TASK_ID=$(curl -s -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "3",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "messageId": "conv-001",
            "role": "user",
            "parts": [{"kind": "text", "text": "I need help with math"}]
        }
    }
  }' | jq -r '.result.id')

# Follow-up message
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d "{
    \"id\": \"4\",
    \"jsonrpc\": \"2.0\",
    \"method\": \"message/send\",
    \"params\": {
        \"message\": {
            \"messageId\": \"conv-002\",
            \"taskId\": \"$TASK_ID\",
            \"role\": \"user\",
            \"parts\": [{\"kind\": \"text\", \"text\": \"Calculate 50 divided by 2\"}]
        }
    }
  }"
```

**Expected Behavior:**
- Second message builds on first conversation
- Agent maintains context from previous interaction
- Both messages appear in task history

### 5. Error Handling Tests

#### Invalid JSON
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{"invalid": json}'
```

#### Missing Required Fields
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "5",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "role": "user"
        }
    }
  }'
```

#### Non-existent Task Update
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "6",
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
        "message": {
            "messageId": "error-001",
            "taskId": "non-existent-task-id",
            "role": "user",
            "parts": [{"kind": "text", "text": "Continue conversation"}]
        }
    }
  }'
```

### 6. Streaming Tests

#### Stream Response
```bash
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{
    "id": "7",
    "jsonrpc": "2.0",
    "method": "message/stream",
    "params": {
        "message": {
            "messageId": "stream-001",
            "role": "user",
            "parts": [{"kind": "text", "text": "Write a short poem about AI"}]
        }
    }
  }'
```

**Expected:** Server-Sent Events stream with incremental updates

## Verification Checklist

### ✅ Integration Verification

- [ ] Server starts without errors
- [ ] A2A endpoints are registered
- [ ] JSON-RPC processor handles requests
- [ ] Agent card contains correct skills
- [ ] Math operations work through A2A protocol
- [ ] Multi-turn conversations maintain context
- [ ] Error handling returns proper A2A responses
- [ ] Streaming endpoints function correctly

### ✅ Functional Testing

- [ ] **Text Processing**: Basic chat messages work
- [ ] **Plugin Integration**: MathPlugin functions accessible
- [ ] **Memory Persistence**: Conversations continue across messages
- [ ] **Error Recovery**: Failed tasks report errors properly
- [ ] **Resource Cleanup**: Completed tasks don't leak memory

### ✅ Protocol Compliance

- [ ] **JSON-RPC 2.0**: Proper request/response format
- [ ] **A2A Specification**: Follows A2A 0.2.6 protocol
- [ ] **Task Lifecycle**: Correct state transitions
- [ ] **Message Format**: Valid A2A message structure
- [ ] **Artifact Creation**: Proper artifact generation

## Troubleshooting

### Common Issues

#### 1. "Connection Refused" Error
**Cause:** Server not running or wrong port
**Solution:** 
```bash
cd /path/to/AgentWebTest/AgentAPI
dotnet run
```

#### 2. "JsonTypeInfo metadata not provided"
**Cause:** Missing JSON serialization context for A2A types
**Solution:** Ensure `AppJsonSerializerContext.cs` includes all A2A types

#### 3. "Ambiguous route" Error
**Cause:** Duplicate endpoint registrations
**Solution:** Remove duplicate `MapGet` calls for the same route

#### 4. "Task Creation Failed"
**Cause:** A2AHandler not properly initialized
**Solution:** Verify `A2AHandler` constructor is called with valid agent and task manager

#### 5. Empty or Missing Response
**Cause:** Agent configuration issues or missing API keys
**Solution:** Check `appsettings.json` for provider configuration

### Debug Logging

Enable detailed logging in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "A2A": "Debug",
      "HPD_Agent": "Debug"
    }
  }
}
```

### Health Check Commands

```bash
# Check if A2A endpoints are mapped
curl -X OPTIONS http://localhost:5135/a2a-agent

# Test basic JSON-RPC format
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{"id":"health","jsonrpc":"2.0","method":"system/ping"}'

# Verify agent plugins are loaded
curl -X POST http://localhost:5135/a2a-agent \
  -H "Content-Type: application/json" \
  -d '{"id":"skills","jsonrpc":"2.0","method":"agent/card"}' | jq '.result.skills'
```

## Performance Testing

### Load Testing Example
```bash
# Test concurrent requests
for i in {1..10}; do
  curl -X POST http://localhost:5135/a2a-agent \
    -H "Content-Type: application/json" \
    -d "{\"id\":\"$i\",\"jsonrpc\":\"2.0\",\"method\":\"message/send\",\"params\":{\"message\":{\"messageId\":\"load-$i\",\"role\":\"user\",\"parts\":[{\"kind\":\"text\",\"text\":\"Calculate $i times 2\"}]}}}" &
done
wait
```

### Memory Usage Monitoring
```bash
# Monitor active conversations
# Check task manager memory usage
# Verify conversation cleanup after task completion
```

## Integration Validation

To validate the complete integration:

1. **Start AgentAPI** server
2. **Run all test categories** above
3. **Verify responses** match expected formats
4. **Check logs** for any errors or warnings
5. **Test edge cases** and error conditions
6. **Validate performance** under load

The integration is successful when all tests pass and the agent responds correctly to A2A protocol messages while maintaining its existing AGUI functionality.