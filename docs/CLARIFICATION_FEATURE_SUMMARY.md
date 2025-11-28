# Clarification Feature - Implementation Summary

## Overview

Successfully implemented a clarification feature that enables parent/orchestrator agents to ask users for information mid-turn without losing context or ending the message turn.

## What Was Implemented

### 1. Core Event Types
**File**: `Agent.cs` (lines 5024-5061)

- `IClarificationEvent` - Marker interface for clarification events
- `ClarificationRequestEvent` - Request with question, requestId, agentName, options
- `ClarificationResponseEvent` - Response with answer

### 2. Event Data for UI Handlers
**File**: `Agent.cs` (lines 5346-5389)

- `ClarificationEventData` class - Contains all event information for UI
- `ClarificationEventType` enum - Request/Response types

### 3. Event Handler Integration
**File**: `Agent.cs` (lines 4031-4070)

- Added clarification event conversion in EventStreamAdapter
- Updates `ExtendedAgentRunResponseUpdate.ClarificationData` property

### 4. ClarificationFunction
**File**: `HumanInTheLoop/ClarificationFunction.cs`

- Static class providing `Create()` method
- Returns `AIFunction` that parent agents can register
- Uses `Agent.CurrentFunctionContext` to access execution context
- Emits events and waits for responses via bidirectional coordination

### 5. Context Enhancement
**File**: `Agent.cs` (line 2284)

- Modified `Agent.CurrentFunctionContext` to store full `AiFunctionContext`
- Enables plugins/functions to access `Emit()` and `WaitForResponseAsync()`

### 6. Documentation
**Files**:
- `docs/CLARIFICATION_FUNCTION_USAGE.md` - Complete usage guide
- `docs/CLARIFICATION_FEATURE_SUMMARY.md` - This summary

## Architecture: Function-Based Instead of Filter-Based

**Key Insight**: ClarificationFunction reuses the existing `BidirectionalEventCoordinator` infrastructure, but as an **opt-in function** rather than an **automatic filter**.

### Why Function, Not Filter?

| Aspect | Filter (PermissionMiddleware) | Function (ClarificationFunction) |
|--------|---------------------------|----------------------------------|
| Execution | Automatic on every call | LLM decides when to call |
| Control | Enforced policy | Intelligent choice |
| Use Case | Security/validation | Optional clarification |
| Access | `context.Emit()` (injected) | `Agent.CurrentFunctionContext` (ambient) |
| Infrastructure | ✅ BidirectionalEventCoordinator | ✅ Same coordinator! |

**See**: [BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md](BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md) for complete details.

## Key Design Decisions

### 1. Parent Agent Calls Clarification, Not Sub-Agent

**Why**: Gives parent the intelligence to:
- Answer questions directly if it knows
- Ask other agents for help
- Only bother the user when truly necessary

**Flow**:
```
User → Orchestrator → SubAgent → returns "Need info?"
     → Orchestrator → sees question → AskUserForClarification
     → User answers → Orchestrator → SubAgent (with answer)
```

### 2. AgentName Field for Parallel Execution

When orchestrator makes parallel calls, the `AgentName` field identifies who's asking:
```
Parallel calls:
- SubAgent1() → takes 20 sec
- SubAgent2() → takes 5 sec
- AskUserForClarification() → asks immediately

User sees: "[Orchestrator] What framework?"
AgentName = "Orchestrator" (who called the function)
```

### 3. Same Limitation as Gemini CLI

Both frameworks use `Promise.all` / `Task.WhenAll`, which means all parallel sub-agents must complete before the next agentic turn. This is a fundamental constraint of the LLM function-calling paradigm.

## Usage Example

```csharp
// Setup
var orchestrator = new Agent(
    name: "Orchestrator",
    instructions: "If sub-agents return questions you can't answer, ask me.");

var codingAgent = new Agent(
    name: "CodingAgent",
    instructions: "Return questions when you need info.");

// Register on PARENT
orchestrator.AddFunction(codingAgent.AsAIFunction());
orchestrator.AddFunction(ClarificationFunction.Create());

// Flow
await orchestrator.RunAsync("Build authentication");
// → Orchestrator calls CodingAgent
// → CodingAgent returns: "Need framework?"
// → Orchestrator calls AskUserForClarification
// → User answers: "Express"
// → Orchestrator calls CodingAgent("Build Express auth")
// → All in ONE message turn!
```

## Handler Implementation

```csharp
await foreach (var evt in orchestrator.RunAsync(query))
{
    switch (evt)
    {
        case ClarificationRequestEvent clarification:
            var agentLabel = clarification.AgentName ?? "Agent";
            Console.WriteLine($"\n[{agentLabel}] {clarification.Question}");
            var answer = Console.ReadLine();

            orchestrator.SendFilterResponse(clarification.RequestId,
                new ClarificationResponseEvent(
                    clarification.RequestId,
                    clarification.SourceName,
                    clarification.Question,
                    answer ?? string.Empty));
            break;
    }
}
```

## Benefits Over Alternatives

### vs. Ending Message Turn
- ✅ No context loss
- ✅ No iteration reset
- ✅ Continues in same turn
- ✅ More efficient

### vs. Sub-Agent Direct User Communication
- ✅ Parent can intercept and answer
- ✅ Parent can ask other agents
- ✅ More intelligent orchestration
- ✅ Follows principle of least privilege

## Unique Capability

**Unlike Gemini CLI and other frameworks**, this enables:
1. Mid-turn user clarification without ending the message turn
2. Parent agent intelligence layer (can answer or delegate)
3. Works in parallel execution scenarios
4. Full event bubbling via AsyncLocal

Gemini CLI explicitly prevents sub-agent user interaction because they lack the event bubbling infrastructure. HPD-Agent's bidirectional event coordination makes this possible.

## Files Modified

1. `/HPD-Agent/Agent/Agent.cs` - Events, data classes, handler support, context storage
2. `/HPD-Agent/HumanInTheLoop/ClarificationFunction.cs` - New file
3. `/HPD-Agent/Filters/AiFunctionOrchestrationContext.cs` - (No changes needed, already supported)
4. `/docs/CLARIFICATION_FUNCTION_USAGE.md` - Complete usage documentation
5. `/docs/CLARIFICATION_FEATURE_SUMMARY.md` - This file

## Testing Checklist

- [ ] Single clarification in orchestrator
- [ ] Multiple clarifications in sequence
- [ ] Parallel sub-agents + clarification
- [ ] Multiple parallel clarifications
- [ ] Nested agents (3+ levels deep)
- [ ] Timeout handling
- [ ] Cancellation handling
- [ ] UI event display (Console/AGUI)

## Related Documentation

- **[BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md](BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md)** - Complete architecture of the shared event infrastructure
- **[CLARIFICATION_FUNCTION_USAGE.md](CLARIFICATION_FUNCTION_USAGE.md)** - Usage guide with examples
- **[BIDIRECTIONAL_FILTER_DEADLOCK_FIX.md](BIDIRECTIONAL_FILTER_DEADLOCK_FIX.md)** - How polling prevents deadlocks
- **[USING_ASAIFUNCTION_WITH_EVENT_BUBBLING.md](USING_ASAIFUNCTION_WITH_EVENT_BUBBLING.md)** - Event bubbling in nested agents
- **[FILTER_EVENTS_USAGE.md](../HPD-Agent/Filters/FILTER_EVENTS_USAGE.md)** - Filter-based event patterns

## Future Enhancements

Possible extensions (not implemented):
- Pre-defined answer options with validation
- Multi-turn clarification dialogues
- Clarification priority/urgency levels
- Rich media clarifications (images, files, etc.)
