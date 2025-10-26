# Clarification Function Usage Guide

## Overview

The `ClarificationFunction` enables parent/orchestrator agents to ask users for clarification **mid-turn** when sub-agents return questions that the parent cannot answer on its own. This is a unique capability that leverages the bidirectional event coordination system to enable human-in-the-loop workflows without ending the message turn.

## Key Concept: Who Calls What?

**IMPORTANT**: The clarification function is registered on and called BY the **parent/orchestrator agent**, NOT the sub-agent!

### The Flow

1. **User** asks: "Build authentication system"
2. **Orchestrator** calls **CodingAgent**("Build auth")
3. **CodingAgent** returns: "I need to know which framework: Express, FastAPI, or Django?"
4. **Orchestrator** receives this as a tool result and realizes it doesn't know the answer
5. **Orchestrator** calls `AskUserForClarification`("Which framework?")
6. **User** answers: "Express"
7. **Orchestrator** continues in the SAME turn, calls **CodingAgent**("Build Express auth")

### Why This Design?

This gives the parent agent the **option** to:
- Answer the question directly if it knows (without bothering the user)
- Ask the user for clarification
- Call another agent for help
- Make an intelligent decision

## How It Works

1. **Parent/Orchestrator agent** registers both:
   - Sub-agent(s) as functions (via `AsAIFunction()`)
   - The clarification function (via `ClarificationFunction.Create()`)

2. **Sub-agent** returns a question string to the parent (via normal function return value)

3. **Parent agent's LLM** sees the question and decides to call `AskUserForClarification`

4. **Clarification request** emits an `InternalClarificationRequestEvent` that bubbles to the root agent

5. **User** receives the question via their UI (AGUI, Console, etc.)

6. **User's answer** flows back via `InternalClarificationResponseEvent`

7. **Parent agent** receives the answer and continues execution in the same turn

## Basic Usage

```csharp
// Create orchestrator and sub-agent
var orchestrator = new Agent(
    name: "Orchestrator",
    instructions: "You coordinate tasks. If a sub-agent returns a question, ask me for clarification.",
    model: model);

var codingAgent = new Agent(
    name: "CodingAgent",
    instructions: "You build code. If you need information, return a question to the caller.",
    model: model);

// Register sub-agent AND clarification function ON THE ORCHESTRATOR
orchestrator.AddFunction(codingAgent.AsAIFunction());
orchestrator.AddFunction(ClarificationFunction.Create());

// Usage flow:
var response = await orchestrator.RunAsync(
    "Build an authentication system",
    cancellationToken: cancellationToken);

// What happens internally:
// 1. Orchestrator calls CodingAgent("Build auth")
// 2. CodingAgent returns: "I need to know which framework..."
// 3. Orchestrator sees this question and calls AskUserForClarification
// 4. User answers
// 5. Orchestrator calls CodingAgent again with the answer
```

## Parent Agent Perspective

From the orchestrator's perspective, `AskUserForClarification` appears as a regular tool:

```
Orchestrator's Available tools:
- CodingAgent(query: string): Invoke coding agent
- AskUserForClarification(question: string): Ask user for information

Example LLM reasoning:
"The coding agent returned a question about frameworks. I don't know the answer,
so I should ask the user for clarification."

<tool_call>
  <name>AskUserForClarification</name>
  <arguments>
    <question>Which framework should we use: Express, FastAPI, or Django?</question>
  </arguments>
</tool_call>
```

## Advanced Usage: Parallel Execution

When the orchestrator calls multiple tools in parallel (sub-agents + clarifications), the `AgentName` field helps identify the source:

```csharp
var orchestrator = new Agent(
    name: "Orchestrator",
    instructions: "You can call multiple agents in parallel if needed.",
    ...);

var researchAgent = new Agent(name: "ResearchAgent", ...);
var codingAgent = new Agent(name: "CodingAgent", ...);

// Register multiple agents
orchestrator.AddFunction(researchAgent.AsAIFunction());
orchestrator.AddFunction(codingAgent.AsAIFunction());
orchestrator.AddFunction(ClarificationFunction.Create());

// Example scenario:
// Orchestrator's LLM decides to make parallel calls:
// - Call ResearchAgent("Find best practices")  → Returns: "Need tech stack info"
// - Call CodingAgent("Build structure")        → Returns: "Need database choice"
// - Call AskUserForClarification("What auth provider?")

// All execute in parallel via Task.WhenAll
// User sees:
// - [Orchestrator] What auth provider?  ← From direct clarification call
//
// After all complete, Orchestrator can make more clarifications:
// - [Orchestrator] Which tech stack?    ← Based on ResearchAgent's return
// - [Orchestrator] Which database?      ← Based on CodingAgent's return
```

The `AgentName` field in the event shows "Orchestrator" since that's who called the clarification function.

## Advanced Usage: Multiple Nested Levels

The clarification function works across arbitrary nesting depths:

```csharp
var orchestrator = new Agent(...);
var planningAgent = new Agent(...);
var researchAgent = new Agent(...);

// Orchestrator can call PlanningAgent
orchestrator.AddFunction(planningAgent.AsAIFunction());
orchestrator.AddFunction(ClarificationFunction.Create());

// PlanningAgent can call ResearchAgent
planningAgent.AddFunction(researchAgent.AsAIFunction());
planningAgent.AddFunction(ClarificationFunction.Create());

// Now:
// User → Orchestrator → PlanningAgent → ResearchAgent
//                                      ↓ (returns question)
//         Orchestrator ← PlanningAgent ← (sees question, calls AskUserForClarification)
// User ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← (event bubbles up)
```

## Why Not Just Have Sub-Agents Return Questions?

**You already can!** Sub-agents can return question strings, and the parent agent's LLM can:
1. Answer directly if it knows
2. Ask another agent for help
3. Call `AskUserForClarification` if it doesn't know

### Without ClarificationFunction:
```
Turn 0: Orchestrator calls CodingAgent → CodingAgent returns "Need framework?"
Turn 1: Orchestrator ends turn, returns to user: "What framework?"
(NEW MESSAGE TURN - context resets)
Turn 0: User answers "Express", Orchestrator calls CodingAgent("Build Express auth")
```

### With ClarificationFunction:
```
Turn 0: Orchestrator calls CodingAgent → CodingAgent returns "Need framework?"
Turn 1: Orchestrator calls AskUserForClarification → User answers "Express"
Turn 2: Orchestrator calls CodingAgent("Build Express auth")
(ALL IN ONE MESSAGE TURN - no context loss)
```

**Key Benefit**: No message turn boundary! The orchestrator maintains full context and continues execution.

## Comparison: Three Communication Patterns

| Pattern | When to Use | How It Works | Turns | Context |
|---------|-------------|--------------|-------|---------|
| **Sub-agent Return Value** | Parent might know answer | Sub-agent returns question string | Parent's next turn | ✅ Maintained |
| **ClarificationFunction** | Parent doesn't know, needs user | Parent calls `AskUserForClarification` | Same turn, blocks mid-execution | ✅ Maintained |
| **Message Turn End** | Traditional approach | End turn, user responds, new turn | New message turn | ❌ Resets |

## Event Flow

```
┌─────────────┐
│    User     │
└──────┬──────┘
       │ "Research best framework"
       ↓
┌─────────────────┐
│  Orchestrator   │  (Root Agent)
│  Agent.RootAgent = orchestrator
└──────┬──────────┘
       │ calls ResearchAgent(query)
       ↓
┌─────────────────┐
│ Research Agent  │  (Nested Agent)
│  Agent.RootAgent still = orchestrator (AsyncLocal!)
└──────┬──────────┘
       │ needs clarification
       ↓
       calls AskUserForClarification("What language?")
       ↓
┌──────────────────────────────────┐
│ ClarificationFunction.Execute()  │
│ - Gets Agent.CurrentFunctionContext
│ - context.Emit(ClarificationRequest) ──┐
│ - await context.WaitForResponse()      │
└────────────────────────────────────────┘
                                          │
                    ┌─────────────────────┘
                    │ Event bubbles via AsyncLocal
                    ↓
┌─────────────────────────────────────────┐
│ Orchestrator.EventCoordinator           │
│ - Receives InternalClarificationRequest │
│ - Yields to event handler (AGUI/Console)│
└──────┬──────────────────────────────────┘
       │
       ↓
┌─────────────┐
│    User     │ sees: "What language?"
│             │ responds: "C# with .NET 8"
└──────┬──────┘
       │
       ↓
┌─────────────────────────────────────────┐
│ Event Handler sends Response            │
│ InternalClarificationResponseEvent      │
└──────┬──────────────────────────────────┘
       │
       ↓
┌──────────────────────────────────┐
│ ClarificationFunction resumes    │
│ - Receives response              │
│ - Returns "C# with .NET 8"       │
└──────┬───────────────────────────┘
       │
       ↓
┌─────────────────┐
│ Research Agent  │ continues with answer
└─────────────────┘
```

## Comparison with Permissions

| Feature | Permissions | Clarifications |
|---------|------------|----------------|
| **Purpose** | Gate/approve function execution | Gather additional information |
| **Trigger** | Before dangerous operations | When information is missing |
| **Implementation** | PermissionFilter (middleware) | ClarificationFunction (callable tool) |
| **Agent calls it?** | No (automatic via filter) | Yes (explicit tool call) |
| **Bubbling** | ✅ Via AsyncLocal | ✅ Via AsyncLocal |
| **Event Type** | IPermissionEvent | IClarificationEvent |

## Handler Implementation

UI handlers need to process `InternalClarificationRequestEvent`:

```csharp
await foreach (var evt in agent.RunAsync(query))
{
    switch (evt)
    {
        case InternalClarificationRequestEvent clarification:
            // Show question to user with agent name (important for parallel sub-agents!)
            var agentLabel = clarification.AgentName ?? "Agent";
            Console.WriteLine($"\n[{agentLabel}] needs clarification:");
            Console.WriteLine($"Question: {clarification.Question}");
            Console.Write("Your answer: ");
            var answer = Console.ReadLine();

            // Send response back using existing SendFilterResponse method
            agent.SendFilterResponse(clarification.RequestId,
                new InternalClarificationResponseEvent(
                    clarification.RequestId,
                    clarification.SourceName,
                    clarification.Question,
                    answer ?? string.Empty));
            break;
    }
}
```

## Key Advantages

1. **No Framework Modifications**: Uses existing event bubbling infrastructure
2. **Arbitrary Nesting**: Works at any depth due to AsyncLocal propagation
3. **Protocol Agnostic**: Works with Console, AGUI, Web, API handlers
4. **Type Safe**: Strongly typed events with correlation IDs
5. **Timeout Handling**: Graceful degradation if user doesn't respond
6. **Unique in Industry**: No other agent framework supports nested human-in-the-loop clarifications

## Why Other Frameworks Can't Do This

**Gemini CLI** explicitly prevents this:
```typescript
// From Gemini CLI source
finalPrompt += `You CANNOT ask the user for input or clarification.`
```

Why? Because they don't have event bubbling infrastructure. A clarification request would:
1. Block the sub-agent execution
2. Never reach the user (no bubbling path)
3. Cause a deadlock

**HPD-Agent** solves this with:
- AsyncLocal-based event bubbling (Agent.RootAgent)
- Bidirectional event coordination (emit + wait pattern)
- Background event drainer (events flow while blocking)

This makes nested human-in-the-loop interactions possible for the first time in agent frameworks.
