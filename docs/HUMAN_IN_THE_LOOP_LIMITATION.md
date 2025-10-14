# Human-in-the-Loop Limitation: Nested Agent Clarification Requests

## Status: **KNOWN LIMITATION** ⚠️

**Last Updated:** 2025-01-13
**Severity:** High - Affects multi-agent orchestration workflows
**Tracking Issue:** TBD

---

## Executive Summary

The current agent architecture **cannot pause an agent turn mid-execution** to request user input. This creates a fundamental limitation when orchestrating multiple agents where a nested agent (e.g., CodingAgent) needs user clarification during execution.

**Impact:**
- Multi-agent systems cannot seamlessly handle nested clarification requests
- Agent-to-agent delegation breaks down when user input is required
- Forces workarounds that compromise agent autonomy or user experience

---

## The Problem: Agent Turns vs. Message Turns

### Key Concepts

1. **Message Turn**: A single user message → agent response cycle
   - User sends message
   - Agent processes and responds
   - Turn completes

2. **Agent Turn**: A single iteration within the agentic loop (internal to message turn)
   - LLM generates response (may include tool calls)
   - Agent executes tools
   - Loop continues or exits
   - Multiple agent turns can happen within one message turn

### The Architectural Constraint

The agentic loop in `Agent.cs:RunAgenticLoopInternal()` runs **atomically** until completion:

```csharp
// Agent.cs:212-225
var internalStream = RunAgenticLoopInternal(
    messages, options, ..., cancellationToken);

// ❌ This consumes ENTIRE stream synchronously
await foreach (var _ in internalStream.WithCancellation(cancellationToken))
{
    // Just drain the stream
}

// Only after ALL agent turns complete
return new ChatResponse(assistantMessages);
```

**The loop cannot pause and wait for external user input mid-execution.**

---

## The Edge Case Scenario

### Setup: Orchestrator + Specialized Agent

```csharp
// Orchestrator delegates tasks to specialized agents
var orchestratorConfig = new AgentConfig
{
    Name = "Orchestrator",
    SystemInstructions = "You delegate tasks to specialized agents",
    MaxAgenticIterations = 20
};

var codingAgent = CreateCodingAgent();
var codingConversation = new Conversation(codingAgent);
var codingTool = codingConversation.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "CodingAgent",
        Description = "Specialized agent for coding tasks"
    },
    thread: null  // Stateless or stateful - doesn't matter for this issue
);

var orchestratorAgent = new AgentBuilder(orchestratorConfig)
    .WithTool(codingTool)  // Register coding agent as a tool
    .Build();
```

### Scenario: Vague User Request Requiring Clarification

**User Message:** "Build me a user authentication system"

**Desired Behavior (NOT POSSIBLE):**

```
1. User → Orchestrator: "Build authentication system"
2. Orchestrator (Agent Turn 0): Analyzes request, calls CodingAgent
3. CodingAgent receives: "Build authentication system"
4. CodingAgent needs clarification: "Which framework? (Express/FastAPI/Django)"
5. ⚠️ PAUSE NEEDED: CodingAgent needs to ask user a question
6. Orchestrator recognizes: "I need to ask the user for clarification"
7. ⚠️ PAUSE AGENT TURN: Stop execution, request user input
8. User receives: "The coding agent needs to know: which framework?"
9. User responds: "Use Express"
10. ⚠️ RESUME AGENT TURN: Continue from where we left off
11. Orchestrator → CodingAgent: "Use Express framework"
12. CodingAgent: Proceeds with implementation ✅
```

**What Actually Happens:**

```
1. User → Orchestrator: "Build authentication system"
2. Orchestrator (Agent Turn 0): Calls CodingAgent
3. CodingAgent returns: "I need to know which framework (Express/FastAPI/Django)"
4. Orchestrator (Agent Turn 1): Receives tool result
5. Orchestrator generates response: "The coding agent needs clarification..."
6. ❌ Agent turn COMPLETES (no more tool calls)
7. User receives: "Which framework do you want?"
8. User responds: "Use Express"
9. ❌ NEW MESSAGE TURN - All context lost!
10. Orchestrator starts fresh: "Build Express authentication"
11. If CodingAgent was stateless (thread: null), it has NO memory of previous conversation
```

---

## Why This Fails: Technical Analysis

### The Agentic Loop is Atomic

From `Agent.cs:629-850`, the agentic loop structure:

```csharp
int iteration = 0;
while (iteration < agentRunContext.MaxIterations)
{
    // 1. LLM Turn (Agent Turn)
    await foreach (var update in _agentTurn.RunAsync(currentMessages, scopedOptions, ct))
    {
        // Collect text, reasoning, function calls
        if (content is FunctionCallContent functionCall)
        {
            toolRequests.Add(functionCall);
        }
    }

    // 2. Execute tool requests
    if (toolRequests.Count > 0)
    {
        var toolResultMessage = await _toolScheduler.ExecuteToolsAsync(...);
        currentMessages.Add(toolResultMessage);
        // ⚠️ Loop continues with results - CANNOT pause here
    }
    else
    {
        // No tool calls = done
        break;
    }

    iteration++;
}

// ⚠️ ONLY AFTER LOOP COMPLETES do we return response
return new ChatResponse(assistantMessages);
```

**Key Problems:**

1. **No Pause Mechanism**: The loop has no way to signal "I need user input"
2. **No State Serialization**: Cannot save current iteration state for later resumption
3. **No Event for User Input**: No `InternalUserInputNeededEvent` exists
4. **Synchronous Consumption**: `GetResponseAsync` drains entire stream before returning

### What Happens During Nested Agent Call

When orchestrator calls `CodingAgent` via `AsAIFunction()`:

```csharp
// Inside AsAIFunction wrapper (created by AgentExtensions.cs:67-85)
async Task<string> InvokeAgentAsync(string query, CancellationToken ct)
{
    // This executes COMPLETELY before returning to orchestrator
    var response = await codingConversation.RunAsync(
        query,  // "Build authentication system"
        thread: thread,
        ct
    );

    // CodingAgent's ENTIRE agentic loop completes here
    // If CodingAgent wants to ask a question, it can only:
    // 1. Return text: "I need to know which framework"
    // 2. Make assumptions and proceed
    // ❌ CANNOT pause and request user input

    return response.Text;  // Returns string result to orchestrator
}
```

**The orchestrator receives a simple string result** - it has no mechanism to detect "this result contains a question that needs user input."

---

## Current Workarounds

### Workaround 1: Agent Makes Assumptions (Partial Solution)

**Approach:** Configure the specialized agent with smart defaults and tell it to make reasonable assumptions.

```csharp
var codingAgentConfig = new AgentConfig
{
    SystemInstructions = @"You are a coding agent.

When you need clarification:
1. Check thread metadata for defaults
2. Make reasonable assumptions based on best practices
3. Document your assumptions in the response
4. Only return clarification questions as a LAST RESORT

Default assumptions:
- Framework: Express.js (Node.js)
- Database: PostgreSQL
- Auth: JWT tokens
- Language: TypeScript"
};
```

**Pros:**
- ✅ Works within existing architecture
- ✅ Agent remains autonomous
- ✅ Good user experience (no back-and-forth)

**Cons:**
- ❌ May not match user intent
- ❌ Requires comprehensive default logic
- ❌ Doesn't work for truly user-specific decisions

---

### Workaround 2: Orchestrator Has Domain Knowledge (Partial Solution)

**Approach:** Give the orchestrator enough context to answer nested agent questions directly.

```csharp
var orchestratorConfig = new AgentConfig
{
    SystemInstructions = @"You are an orchestrator with knowledge:

Project Standards:
- Framework: Express.js
- Database: PostgreSQL
- Auth: JWT tokens

When delegating to CodingAgent, if you receive clarification questions
and you have reasonable defaults from project standards, answer them
directly WITHOUT asking the user.

Only ask the user if:
1. You don't have the information
2. The decision significantly impacts the project
3. The user explicitly needs to make the choice"
};
```

**Pros:**
- ✅ Works within existing architecture
- ✅ Reduces user interruptions
- ✅ Maintains consistency with project standards

**Cons:**
- ❌ Orchestrator must know domain-specific details
- ❌ Doesn't scale to arbitrary domains
- ❌ Still can't handle truly novel scenarios

---

### Workaround 3: Multi-Turn Conversation (Accepted UX Pattern)

**Approach:** Accept that some tasks require multiple conversation turns.

```
Turn 1:
User: "Build authentication"
Orchestrator → CodingAgent: "Build authentication"
CodingAgent: "Need framework choice"
Orchestrator → User: "Which framework? (Express/FastAPI/Django)"

Turn 2:
User: "Express"
Orchestrator → CodingAgent: "Build Express authentication"
CodingAgent: Proceeds ✅
```

**Pros:**
- ✅ Works with current architecture
- ✅ No code changes needed
- ✅ User maintains control

**Cons:**
- ❌ Breaks agent autonomy
- ❌ Poor UX (feels fragmented)
- ❌ Context may be lost between turns (depending on thread strategy)

---

### Workaround 4: Shared Thread with Context Preservation

**Approach:** Use stateful agent-as-tool pattern with shared thread to preserve conversation history.

```csharp
// Create per-user thread for coding agent
var codingThread = new ConversationThread();
codingThread.AddMetadata("Framework", "Express");
codingThread.AddMetadata("Database", "PostgreSQL");

var codingTool = codingConversation.AsAIFunction(
    options,
    thread: codingThread  // All calls share this thread
);
```

**Pros:**
- ✅ Preserves context across calls
- ✅ Agent can reference previous conversation
- ✅ Works for iterative workflows

**Cons:**
- ❌ Still requires multi-turn for user input
- ❌ Doesn't solve the fundamental pause problem
- ❌ Thread management complexity

---

## What Other Systems Do

### OpenAI Assistants API: `required_action` State

OpenAI has a mechanism for pausing execution:

```json
{
  "id": "run_abc123",
  "status": "requires_action",
  "required_action": {
    "type": "submit_tool_outputs",
    "submit_tool_outputs": {
      "tool_calls": [
        {
          "id": "call_abc",
          "function": {
            "name": "get_weather",
            "arguments": "{\"location\": \"San Francisco\"}"
          }
        }
      ]
    }
  }
}
```

**However:** This is for **tool execution**, not **user clarification**.

---

### LangGraph: Conditional State Machine

LangGraph uses conditional edges to route to human input:

```python
from langgraph.graph import StateGraph

workflow = StateGraph(State)

workflow.add_node("coding_agent", coding_agent_node)
workflow.add_node("human_input", human_input_node)

# Conditional routing
workflow.add_conditional_edges(
    "coding_agent",
    should_ask_user,  # Function that returns "user_input" or "continue"
    {
        "user_input": "human_input",  # Pause and wait
        "continue": "coding_agent"    # Keep going
    }
)
```

**Key Feature:** Explicit state machine with pause states.

---

### AutoGen: Human-in-the-Loop Agents

AutoGen has explicit human proxy agents:

```python
from autogen import UserProxyAgent

user_proxy = UserProxyAgent(
    "user_proxy",
    human_input_mode="ALWAYS",  # Ask user on every turn
    max_consecutive_auto_reply=0
)

# Agent explicitly requests human input
coding_agent.initiate_chat(
    user_proxy,
    message="I need to know which framework to use"
)
# ⚠️ Execution PAUSES here, waits for human input
```

**Key Feature:** First-class human-in-the-loop protocol.

---

## Required Implementation: Human-in-the-Loop Protocol

To properly support this scenario, the following architectural changes are needed:

### 1. New Event Type: User Input Request

```csharp
/// <summary>
/// Event emitted when agent needs user input to proceed
/// </summary>
public class InternalUserInputNeededEvent : InternalEvent
{
    public string RequestId { get; set; }
    public string Question { get; set; }
    public Dictionary<string, string>? Options { get; set; }  // Optional choices
    public string? Context { get; set; }

    public InternalUserInputNeededEvent(string requestId, string question)
    {
        RequestId = requestId;
        Question = question;
    }
}
```

### 2. State Serialization for Pause/Resume

```csharp
/// <summary>
/// Represents a paused agent turn that can be resumed
/// </summary>
public class PausedAgentTurn
{
    public string TurnId { get; set; }
    public string ConversationId { get; set; }
    public List<ChatMessage> MessageState { get; set; }
    public int CurrentIteration { get; set; }
    public string UserInputRequestId { get; set; }
    public Dictionary<string, object> AdditionalState { get; set; }
}
```

### 3. Modified Agentic Loop with Pause Support

```csharp
// Pseudocode for modified loop
while (iteration < maxIterations)
{
    // Execute agent turn
    var updates = await _agentTurn.RunAsync(currentMessages, options, ct);

    // Execute tools
    if (toolRequests.Count > 0)
    {
        try
        {
            var toolResults = await _toolScheduler.ExecuteToolsAsync(...);
        }
        catch (UserInputRequiredException ex)
        {
            // ✅ NEW: Pause and request user input
            yield return new InternalUserInputNeededEvent(
                requestId: Guid.NewGuid().ToString(),
                question: ex.Question
            );

            // Serialize state for resumption
            var pausedState = new PausedAgentTurn
            {
                TurnId = messageTurnId,
                ConversationId = conversationId,
                MessageState = currentMessages,
                CurrentIteration = iteration,
                UserInputRequestId = ex.RequestId
            };

            // Return paused state instead of completing
            historyCompletionSource.SetResult(pausedState);
            yield break;  // Exit early, preserving state
        }
    }

    iteration++;
}
```

### 4. Resume API

```csharp
/// <summary>
/// Resume a paused agent turn with user input
/// </summary>
public async Task<ChatResponse> ResumeAgenticTurnAsync(
    PausedAgentTurn pausedTurn,
    string userInput,
    CancellationToken cancellationToken = default)
{
    // Add user's answer to message state
    pausedTurn.MessageState.Add(new ChatMessage(ChatRole.User, userInput));

    // Resume from saved iteration
    return await RunAgenticLoopInternal(
        pausedTurn.MessageState,
        options: null,
        startIteration: pausedTurn.CurrentIteration,
        cancellationToken: cancellationToken
    );
}
```

### 5. Special "AskUser" Tool

```csharp
/// <summary>
/// Plugin that allows agents to request user input
/// </summary>
public class HumanInTheLoopPlugin
{
    [AIFunction]
    [Description("Request input from the user when you need clarification or additional information")]
    public async Task<string> AskUser(
        [Description("Question to ask the user")] string question,
        [Description("Optional: Multiple choice options")] string[]? options = null,
        CancellationToken ct = default)
    {
        // Throw special exception that signals pause is needed
        throw new UserInputRequiredException(question, options)
        {
            RequestId = Guid.NewGuid().ToString()
        };
    }
}

/// <summary>
/// Exception thrown when agent needs user input
/// </summary>
public class UserInputRequiredException : Exception
{
    public string RequestId { get; set; }
    public string Question { get; set; }
    public string[]? Options { get; set; }

    public UserInputRequiredException(string question, string[]? options = null)
        : base($"User input required: {question}")
    {
        Question = question;
        Options = options;
    }
}
```

### 6. Usage in Nested Agents

```csharp
// CodingAgent can now explicitly request user input
var codingAgentConfig = new AgentConfig
{
    SystemInstructions = @"You are a coding agent.

When you need user input that you truly cannot infer:
1. Use the AskUser function
2. Provide a clear question
3. Optionally provide multiple choice options
4. Wait for the response before proceeding"
};

var codingAgent = new AgentBuilder(codingAgentConfig)
    .WithPlugin<HumanInTheLoopPlugin>()  // ✅ Enable human-in-the-loop
    .WithPlugin<FileSystemPlugin>()
    .Build();
```

### 7. Orchestrator Handling

The orchestrator automatically handles the pause/resume:

```csharp
await foreach (var update in orchestrator.RunStreamingAsync([userMessage]))
{
    if (update is InternalUserInputNeededEvent inputNeeded)
    {
        // ✅ Agent turn paused, requesting user input
        Console.Write($"\n{inputNeeded.Question}\nYour answer: ");
        var answer = Console.ReadLine();

        // Resume the paused turn
        var pausedTurn = await orchestrator.GetPausedTurnAsync(inputNeeded.RequestId);
        await orchestrator.ResumeAgenticTurnAsync(pausedTurn, answer);
    }
    else
    {
        // Normal streaming
        RenderUpdate(update);
    }
}
```

---

## Impact Analysis

### Current State
- ❌ Cannot pause agent turn for user input
- ❌ Nested agent clarifications break orchestration
- ⚠️ Must use workarounds (assumptions, multi-turn, domain knowledge)

### After Implementation
- ✅ Agents can explicitly request user input
- ✅ Agent turns can pause and resume
- ✅ True human-in-the-loop workflows
- ✅ Nested agents can ask clarifying questions
- ✅ Better UX for complex multi-agent tasks

---

## Recommendations

### Short-term (Current System)

1. **Document workarounds** for users (this document)
2. **Use smart defaults** in specialized agents
3. **Give orchestrators domain knowledge** where possible
4. **Accept multi-turn patterns** for complex clarifications

### Medium-term (6-12 months)

1. **Implement `UserInputRequiredException`** and special tool
2. **Add pause/resume state serialization**
3. **Create `InternalUserInputNeededEvent`**
4. **Modify agentic loop** to handle pauses

### Long-term (12+ months)

1. **Full state machine workflow engine** (like LangGraph)
2. **Declarative orchestration DSL**
3. **Visual workflow designer**
4. **Advanced human-in-the-loop patterns** (approvals, feedback loops)

---

## References

### Internal Documentation
- `Agent.cs:629-850` - Main agentic loop
- `AgentExtensions.cs:67-85` - `AsAIFunction()` implementation
- `Conversation.cs:114-177` - Conversation `RunAsync` wrapper
- `docs/orchestration-framework.md` - Multi-agent patterns

### External Systems
- [OpenAI Assistants API - Run Object](https://platform.openai.com/docs/api-reference/runs/object)
- [LangGraph - Human-in-the-Loop](https://langchain-ai.github.io/langgraph/how-tos/human-in-the-loop/)
- [AutoGen - UserProxyAgent](https://microsoft.github.io/autogen/docs/reference/agentchat/user_proxy_agent)
- [PydanticAI - Multi-Agent Applications](https://ai.pydantic.dev/multi-agent-applications/)

---

## Open Questions

1. **State Size:** How large can paused turn state grow? Need limits?
2. **Timeout:** Should paused turns expire after X minutes?
3. **Multi-user:** How to handle paused turns in multi-user systems?
4. **Rollback:** Can users "undo" and restart a paused turn?
5. **Nested Pauses:** What if a resumed turn needs to pause again?
6. **Persistence:** Should paused turns be persisted to database?

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-13 | 1.0 | Initial documentation of limitation and proposed solutions |

---

## Acknowledgments

This limitation was identified through real-world multi-agent orchestration scenarios where nested agents require user clarification mid-execution. The proposed solutions draw inspiration from LangGraph's state machine approach and AutoGen's human-in-the-loop patterns.
