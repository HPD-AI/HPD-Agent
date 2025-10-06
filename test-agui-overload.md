# Testing AG-UI Overload Flow

## What We Updated

### 1. **C# Backend (AgentAPI/Program.cs)**
- Updated `/stream` endpoint to accept `RunAgentInput` instead of manual message construction
- Uses `conversation.SendStreamingAsync(RunAgentInput)` overload
- Proper AG-UI protocol support end-to-end

### 2. **Frontend (routes/+page.svelte)**
- Imports AG-UI types: `RunAgentInput`, `Message` from `@ag-ui/client`
- Constructs proper AG-UI input with:
  - `threadId`: conversation ID
  - `runId`: unique UUID per request
  - `messages`: conversation history + new message in AG-UI format
  - `tools`: empty (provided by backend)
  - `context`: empty
  - `state`: empty object
  - `forwardedProps`: empty object

### 3. **Conversation Class (HPD-Agent/Conversation/Conversation.cs)**
- Already has AG-UI overload: `SendStreamingAsync(RunAgentInput, CancellationToken)`
- Delegates to internal `SendStreamingAsyncAGUI` method
- Uses `agent.ExecuteStreamingTurnAsync(RunAgentInput)`

### 4. **Agent Class (HPD-Agent/Agent/Agent.cs)**
- Already has AG-UI overload: `ExecuteStreamingTurnAsync(RunAgentInput, CancellationToken)`
- Converts AG-UI input to Extensions.AI format via `AGUIEventConverter`
- Preserves metadata (ConversationId, RunId)

## Testing Steps

1. **Start the API:**
   ```bash
   cd AgentWebTest/AgentAPI
   dotnet run
   ```

2. **Start the Frontend:**
   ```bash
   cd AgentWebTest/Frontend/frontend
   npm run dev
   ```

3. **Test Flow:**
   - Create a project
   - Create a conversation
   - Send a message
   - Watch the console for AG-UI event types being logged
   - Verify the conversation history is properly maintained across messages

## Expected Behavior

### Backend Flow:
1. Receives `RunAgentInput` JSON from frontend
2. Deserializes using `AGUIJsonContext` (already in Combined resolver)
3. Passes to `Conversation.SendStreamingAsync(RunAgentInput)`
4. Streams AG-UI `BaseEvent` objects back to frontend

### Frontend Flow:
1. Constructs `RunAgentInput` with conversation history
2. POSTs to `/stream` endpoint
3. Receives SSE stream of AG-UI events
4. Handles events by type (TEXT_MESSAGE_CONTENT, THINKING_*, TOOL_CALL_*, etc.)

## Debugging Tips

- Check browser console for message construction logs
- Check backend logs for deserialization errors
- Verify RunAgentInput structure matches between TS and C#
- Ensure all messages have proper `id` and `role` fields
