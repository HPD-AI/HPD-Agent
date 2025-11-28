# AGUI Protocol Strategy Proposal
**Date**: 2025-01-18
**Author**: HPD-Agent Team
**Status**: Planning

---

## Executive Summary

This proposal outlines the strategy for making **AG-UI (Agent-User Interaction Protocol)** the default protocol for HPD-Agent, while maintaining Microsoft.Agents.AI compatibility as an optional adapter. The AGUI protocol provides superior extensibility through `CustomEvent` types, allowing lossless event streaming that preserves all HPD-specific features.

### Key Decisions

1. ✅ **AGUI as Default Protocol** - Provides lossless event streaming via CustomEvent
2. ✅ **Microsoft.Agents.AI as Compatibility Layer** - Optional adapter for Microsoft ecosystem users
3. ✅ **Extended TypeScript Client SDK** - Composition-based extension using AGUI subscribers
4. ✅ **No Protocol Fork Required** - AGUI's CustomEvent mechanism is sufficient

---

## 1. Protocol Comparison & Strategic Rationale

### Why AGUI Over Microsoft.Agents.AI?

| Feature | AGUI Protocol | Microsoft.Agents.AI |
|---------|--------------|---------------------|
| **Event Types** | 19 standard + CustomEvent + RawEvent | ~5 basic types |
| **Extensibility** | ✅ CustomEvent escape hatch | ❌ No extension mechanism |
| **Permission Events** | ✅ Built-in (FunctionPermissionRequestEvent) | ❌ Not supported |
| **Continuation Events** | ✅ Built-in (ContinuationPermissionRequestEvent) | ❌ Not supported |
| **Turn Boundaries** | ✅ STEP_STARTED/FINISHED events | ❌ Not exposed |
| **State Management** | ✅ STATE_SNAPSHOT/DELTA events | ❌ Not exposed |
| **Custom Events** | ✅ CustomEvent + RawEvent | ❌ No escape hatch |
| **Ecosystem** | ✅ Emerging standard (CopilotKit, Pydantic AI) | ⚠️ Microsoft ecosystem only |
| **Event Preservation** | ✅ **Lossless** (all HPD events preserved) | ❌ **Lossy** (filters events) |

### The AGUI Advantage: CustomEvent

```typescript
type CustomEvent = BaseEvent & {
  type: EventType.CUSTOM
  name: string        // Event identifier (e.g., "PermissionRequest")
  value: any          // Arbitrary payload - preserves ALL HPD data
}
```

This allows **100% lossless conversion** of internal HPD events:

```csharp
// HPD Internal Event → AGUI CustomEvent (NO DATA LOSS)
PermissionRequestEvent permReq => new CustomEvent
{
    Type = "CUSTOM",
    Name = "PermissionRequest",
    Data = JsonSerializer.SerializeToElement(new {
        PermissionId = permReq.PermissionId,
        FunctionName = permReq.FunctionName,
        Description = permReq.Description,
        CallId = permReq.CallId,
        Arguments = permReq.Arguments
    })
}
```

### Microsoft Protocol Limitation: Event Filtering

```csharp
// HPD Internal Event → Microsoft Protocol (DATA LOSS)
PermissionRequestEvent permReq => null  // ❌ FILTERED OUT
ClarificationRequestEvent clarReq => null  // ❌ FILTERED OUT
FilterProgressEvent progress => null  // ❌ FILTERED OUT
```

---

## 2. AGUI Adapter Implementation Plan

### Phase 1: Core Adapter Enhancement

**Current State**: Basic AGUI adapter exists at `HPD-Agent\Agent\AGUI\Agent.cs`

**Required Changes**:

#### 2.1 Add Non-Streaming RunAsync Method

Currently AGUI adapter only supports streaming. Add non-streaming API for compatibility:

```csharp
// File: HPD-Agent\Agent\AGUI\Agent.cs

/// <summary>
/// Runs the agent with AGUI protocol input (non-streaming).
/// Collects all events and returns final result.
/// </summary>
public async Task<RunAgentResult> RunAsync(
    RunAgentInput input,
    CancellationToken cancellationToken = default)
{
    var messages = new List<ChatMessage>();
    var events = new List<BaseEvent>();

    // Use channel for event collection
    var channel = Channel.CreateUnbounded<BaseEvent>();

    // Start streaming run in background
    var runTask = RunAsync(input, channel.Writer, cancellationToken);

    // Collect all events
    await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
    {
        events.Add(evt);

        // Extract messages from TEXT_MESSAGE_END events
        if (evt is TextMessageEndEvent textEnd)
        {
            // Reconstruct message from collected events
            // (Implementation details below)
        }
    }

    await runTask;

    return new RunAgentResult
    {
        Messages = messages,
        Events = events
    };
}
```

#### 2.2 Enhance EventStreamAdapter with CustomEvent Mapping

**Current State**: EventStreamAdapter at line 164-236 only maps basic events

**Required Enhancement**: Map ALL HPD internal events to AGUI events (using CustomEvent for HPD-specific ones)

```csharp
// File: HPD-Agent\Agent\AGUI\Agent.cs (EventStreamAdapter class)

public static async IAsyncEnumerable<BaseEvent> ToAGUI(
    IAsyncEnumerable<AgentEvent> internalStream,
    string threadId,
    string runId,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var internalEvent in internalStream.WithCancellation(cancellationToken))
    {
        BaseEvent? aguiEvent = internalEvent switch
        {
            // ===== EXISTING MAPPINGS (Keep as-is) =====

            // MESSAGE TURN → RUN events
            MessageTurnStartedEvent => EventSerialization.CreateRunStarted(threadId, runId),
            MessageTurnFinishedEvent => EventSerialization.CreateRunFinished(threadId, runId),
            MessageTurnErrorEvent e => EventSerialization.CreateRunError(e.Message),

            // AGENT TURN → STEP events
            AgentTurnStartedEvent e => EventSerialization.CreateStepStarted(
                stepId: $"step_{e.Iteration}",
                stepName: $"Iteration {e.Iteration}",
                description: null),
            AgentTurnFinishedEvent e => EventSerialization.CreateStepFinished(
                stepId: $"step_{e.Iteration}",
                stepName: $"Iteration {e.Iteration}",
                result: null),

            // TEXT CONTENT events
            TextMessageStartEvent e => EventSerialization.CreateTextMessageStart(e.MessageId, e.Role),
            TextDeltaEvent e => EventSerialization.CreateTextMessageContent(e.MessageId, e.Text),
            TextMessageEndEvent e => EventSerialization.CreateTextMessageEnd(e.MessageId),

            // REASONING events
            ReasoningStartEvent e => EventSerialization.CreateReasoningStart(e.MessageId),
            ReasoningMessageStartEvent e => EventSerialization.CreateReasoningMessageStart(e.MessageId, e.Role),
            ReasoningDeltaEvent e => EventSerialization.CreateReasoningMessageContent(e.MessageId, e.Text),
            ReasoningMessageEndEvent e => EventSerialization.CreateReasoningMessageEnd(e.MessageId),
            ReasoningEndEvent e => EventSerialization.CreateReasoningEnd(e.MessageId),

            // TOOL events
            ToolCallStartEvent e => EventSerialization.CreateToolCallStart(e.CallId, e.Name, e.MessageId),
            ToolCallArgsEvent e => EventSerialization.CreateToolCallArgs(e.CallId, e.ArgsJson),
            ToolCallEndEvent e => EventSerialization.CreateToolCallEnd(e.CallId),
            ToolCallResultEvent e => EventSerialization.CreateToolCallResult(e.CallId, e.Result),

            // PERMISSION events (Native AGUI support!)
            PermissionRequestEvent e => EventSerialization.CreateFunctionPermissionRequest(
                e.PermissionId,
                e.FunctionName,
                e.Description ?? "",
                e.Arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object?>(),
                new[] { PermissionScope.Conversation, PermissionScope.Project, PermissionScope.Global }),

            ContinuationRequestEvent e => EventSerialization.CreateContinuationPermissionRequest(
                e.ContinuationId,
                e.CurrentIteration,
                e.MaxIterations,
                Array.Empty<string>(),
                ""),

            // ===== NEW MAPPINGS (CustomEvent for HPD-specific events) =====

            // Permission responses (use CustomEvent)
            PermissionResponseEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "PermissionResponse",
                Data = JsonSerializer.SerializeToElement(new {
                    PermissionId = e.PermissionId,
                    Approved = e.Approved,
                    Reason = e.Reason,
                    Choice = e.Choice.ToString()
                }),
                Timestamp = GetTimestamp()
            },

            PermissionApprovedEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "PermissionApproved",
                Data = JsonSerializer.SerializeToElement(new {
                    PermissionId = e.PermissionId
                }),
                Timestamp = GetTimestamp()
            },

            PermissionDeniedEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "PermissionDenied",
                Data = JsonSerializer.SerializeToElement(new {
                    PermissionId = e.PermissionId,
                    Reason = e.Reason
                }),
                Timestamp = GetTimestamp()
            },

            // Continuation responses (use CustomEvent)
            ContinuationResponseEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "ContinuationResponse",
                Data = JsonSerializer.SerializeToElement(new {
                    ContinuationId = e.ContinuationId,
                    Approved = e.Approved,
                    ExtensionAmount = e.ExtensionAmount
                }),
                Timestamp = GetTimestamp()
            },

            // Clarification events (use CustomEvent)
            ClarificationRequestEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "ClarificationRequest",
                Data = JsonSerializer.SerializeToElement(new {
                    RequestId = e.RequestId,
                    AgentName = e.AgentName,
                    Question = e.Question,
                    Options = e.Options
                }),
                Timestamp = GetTimestamp()
            },

            ClarificationResponseEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "ClarificationResponse",
                Data = JsonSerializer.SerializeToElement(new {
                    RequestId = e.RequestId,
                    Question = e.Question,
                    Answer = e.Answer
                }),
                Timestamp = GetTimestamp()
            },

            // Filter events (use CustomEvent)
            FilterProgressEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "FilterProgress",
                Data = JsonSerializer.SerializeToElement(new {
                    SourceName = e.SourceName,
                    Message = e.Message,
                    PercentComplete = e.PercentComplete
                }),
                Timestamp = GetTimestamp()
            },

            FilterErrorEvent e => new CustomEvent
            {
                Type = "CUSTOM",
                Name = "FilterError",
                Data = JsonSerializer.SerializeToElement(new {
                    SourceName = e.SourceName,
                    ErrorMessage = e.ErrorMessage,
                    ExceptionType = e.Exception?.GetType().Name
                }),
                Timestamp = GetTimestamp()
            },

            // State snapshot (use AGUI native StateSnapshotEvent)
            StateSnapshotEvent e => new StateSnapshotEvent
            {
                Type = "STATE_SNAPSHOT",
                State = JsonSerializer.SerializeToElement(e.State),
                Timestamp = GetTimestamp()
            },

            _ => null // Unknown event type
        };

        if (aguiEvent != null)
        {
            yield return aguiEvent;
        }
    }
}

private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
```

### Event Mapping Summary

| HPD Internal Event | AGUI Event Type | Notes |
|-------------------|-----------------|-------|
| `MessageTurnStartedEvent` | `RunStartedEvent` | ✅ Native AGUI |
| `MessageTurnFinishedEvent` | `RunFinishedEvent` | ✅ Native AGUI |
| `AgentTurnStartedEvent` | `StepStartedEvent` | ✅ Native AGUI |
| `AgentTurnFinishedEvent` | `StepFinishedEvent` | ✅ Native AGUI |
| `TextDeltaEvent` | `TextMessageContentEvent` | ✅ Native AGUI |
| `ReasoningDeltaEvent` | `ReasoningMessageContentEvent` | ✅ Native AGUI |
| `ToolCallStartEvent` | `ToolCallStartEvent` | ✅ Native AGUI |
| `PermissionRequestEvent` | `FunctionPermissionRequestEvent` | ✅ Native AGUI |
| `ContinuationRequestEvent` | `ContinuationPermissionRequestEvent` | ✅ Native AGUI |
| `PermissionResponseEvent` | `CustomEvent(PermissionResponse)` | 🔧 CustomEvent |
| `PermissionApprovedEvent` | `CustomEvent(PermissionApproved)` | 🔧 CustomEvent |
| `PermissionDeniedEvent` | `CustomEvent(PermissionDenied)` | 🔧 CustomEvent |
| `ContinuationResponseEvent` | `CustomEvent(ContinuationResponse)` | 🔧 CustomEvent |
| `ClarificationRequestEvent` | `CustomEvent(ClarificationRequest)` | 🔧 CustomEvent |
| `ClarificationResponseEvent` | `CustomEvent(ClarificationResponse)` | 🔧 CustomEvent |
| `FilterProgressEvent` | `CustomEvent(FilterProgress)` | 🔧 CustomEvent |
| `FilterErrorEvent` | `CustomEvent(FilterError)` | 🔧 CustomEvent |
| `StateSnapshotEvent` | `StateSnapshotEvent` | ✅ Native AGUI |

**Result**: 100% lossless event streaming - no HPD events are filtered!

---

## 3. TypeScript Client SDK Strategy

### Architecture Decision: Composition Over Forking

**Decision**: Extend AGUI client SDK via **composition** (subscribers), not forking.

**Rationale**:
1. ✅ AGUI SDK is already extensible via `AgentSubscriber` interface
2. ✅ `onCustomEvent` hook allows handling all HPD events
3. ✅ Maintains compatibility with AGUI ecosystem (UIs, tools)
4. ✅ Inherits upstream improvements automatically
5. ✅ Lower maintenance burden

### Package Structure

```
@hpd-agent/client/
├── src/
│   ├── index.ts                    # Main export
│   ├── agent/
│   │   ├── HPDAgent.ts            # Wrapper around @ag-ui/client
│   │   └── types.ts               # HPD-specific types
│   ├── subscribers/
│   │   ├── HPDSubscriber.ts       # Main subscriber for CustomEvents
│   │   ├── PermissionSubscriber.ts # Permission event handling
│   │   ├── TurnBoundarySubscriber.ts # Turn tracking
│   │   └── FilterProgressSubscriber.ts # Filter progress UI
│   ├── events/
│   │   ├── CustomEventTypes.ts    # TypeScript types for HPD events
│   │   └── EventParsers.ts        # Parse CustomEvent.value
│   └── ui/
│       ├── PermissionDialog.tsx   # Permission request UI
│       ├── FilterProgress.tsx     # Filter progress display
│       └── TurnBoundary.tsx       # Iteration counter
├── package.json
└── tsconfig.json

Dependencies:
- @ag-ui/client (core AGUI SDK)
- rxjs (for reactive streams)
```

### Implementation: HPDSubscriber

```typescript
// File: src/subscribers/HPDSubscriber.ts

import { AgentSubscriber, CustomEvent, AgentSubscriberParams } from '@ag-ui/client';
import { parseHPDEvent, HPDCustomEvent } from '../events/EventParsers';

export interface HPDSubscriberOptions {
  onPermissionRequest?: (data: PermissionRequestData) => Promise<boolean>;
  onClarificationRequest?: (data: ClarificationRequestData) => Promise<string>;
  onFilterProgress?: (data: FilterProgressData) => void;
  onTurnBoundary?: (data: TurnBoundaryData) => void;
  // ... other HPD event handlers
}

export class HPDSubscriber implements AgentSubscriber {
  constructor(private options: HPDSubscriberOptions) {}

  async onCustomEvent(params: { event: CustomEvent } & AgentSubscriberParams) {
    const { event, messages, state } = params;

    // Parse HPD custom event
    const hpdEvent = parseHPDEvent(event);
    if (!hpdEvent) return; // Not an HPD event

    switch (hpdEvent.name) {
      case 'PermissionRequest':
        return await this.handlePermissionRequest(hpdEvent.data, params);

      case 'PermissionResponse':
      case 'PermissionApproved':
      case 'PermissionDenied':
        return this.handlePermissionResponse(hpdEvent, params);

      case 'ClarificationRequest':
        return await this.handleClarificationRequest(hpdEvent.data, params);

      case 'ClarificationResponse':
        return this.handleClarificationResponse(hpdEvent, params);

      case 'FilterProgress':
        this.options.onFilterProgress?.(hpdEvent.data);
        return;

      case 'FilterError':
        console.error('Filter error:', hpdEvent.data);
        return;

      default:
        console.warn('Unknown HPD event:', hpdEvent.name);
    }
  }

  private async handlePermissionRequest(
    data: PermissionRequestData,
    params: AgentSubscriberParams
  ) {
    if (!this.options.onPermissionRequest) {
      // Auto-approve if no handler
      return;
    }

    const approved = await this.options.onPermissionRequest(data);

    // Send response back to agent (via bidirectional FFI)
    // (Implementation depends on your FFI setup)

    return;
  }

  private async handleClarificationRequest(
    data: ClarificationRequestData,
    params: AgentSubscriberParams
  ) {
    if (!this.options.onClarificationRequest) {
      return;
    }

    const answer = await this.options.onClarificationRequest(data);

    // Send response back to agent
    // (Implementation depends on your FFI setup)

    return;
  }

  // ... other handlers
}
```

### Implementation: HPDAgent Wrapper

```typescript
// File: src/agent/HPDAgent.ts

import { HttpAgent, HttpAgentConfig } from '@ag-ui/client';
import { HPDSubscriber, HPDSubscriberOptions } from '../subscribers/HPDSubscriber';

export interface HPDAgentConfig extends HttpAgentConfig, HPDSubscriberOptions {
  // HPD-specific config
}

export class HPDAgent extends HttpAgent {
  private hpdSubscriber: HPDSubscriber;

  constructor(config: HPDAgentConfig) {
    super(config);

    // Auto-attach HPD subscriber
    this.hpdSubscriber = new HPDSubscriber({
      onPermissionRequest: config.onPermissionRequest,
      onClarificationRequest: config.onClarificationRequest,
      onFilterProgress: config.onFilterProgress,
      onTurnBoundary: config.onTurnBoundary,
    });

    this.subscribe(this.hpdSubscriber);
  }
}
```

### Usage Example

```typescript
import { HPDAgent } from '@hpd-agent/client';

const agent = new HPDAgent({
  url: 'https://api.example.com/agent',

  // HPD-specific handlers
  onPermissionRequest: async (data) => {
    // Show permission dialog
    const approved = await showPermissionDialog(data);
    return approved;
  },

  onClarificationRequest: async (data) => {
    // Show clarification prompt
    const answer = await prompt(data.question, data.options);
    return answer;
  },

  onFilterProgress: (data) => {
    // Update progress bar
    updateProgressBar(data.percentComplete);
  },

  onTurnBoundary: (data) => {
    // Update iteration counter
    updateIterationCounter(data.iteration);
  }
});

// Run agent (standard AGUI API)
const result = await agent.runAgent();
```

---

## 4. Implementation Phases

### Phase 1: AGUI Adapter Enhancement (Week 1)
- [ ] Add non-streaming `RunAsync` method to AGUI adapter
- [ ] Enhance `EventStreamAdapter.ToAGUI` with CustomEvent mappings
- [ ] Add helper method `CreateCustomEvent` to EventSerialization
- [ ] Write unit tests for all event mappings
- [ ] Update AGUI adapter documentation

### Phase 2: TypeScript Client SDK (Week 2-3)
- [ ] Set up `@hpd-agent/client` package structure
- [ ] Implement `HPDSubscriber` with CustomEvent parsing
- [ ] Implement `HPDAgent` wrapper class
- [ ] Create TypeScript types for all HPD custom events
- [ ] Write event parser utilities
- [ ] Add comprehensive tests

### Phase 3: UI Components (Week 4-5)
- [ ] Implement `PermissionDialog` React component
- [ ] Implement `ClarificationPrompt` React component
- [ ] Implement `FilterProgress` component
- [ ] Implement `TurnBoundary` iteration counter
- [ ] Create example applications
- [ ] Write component documentation

### Phase 4: Documentation & Examples (Week 6)
- [ ] Write migration guide from Microsoft adapter
- [ ] Create AGUI protocol documentation
- [ ] Add CustomEvent reference documentation
- [ ] Build example applications (chat, IDE extension, etc.)
- [ ] Write integration guides

---

## 5. Benefits & Trade-offs

### Benefits

✅ **Lossless Event Streaming**
- All HPD internal events preserved via CustomEvent
- No feature degradation for advanced users

✅ **Ecosystem Compatibility**
- Works with standard AGUI UIs (CopilotKit, etc.)
- Inherits AGUI ecosystem improvements
- Lower barrier to adoption

✅ **Future-Proof**
- AGUI is emerging standard (not vendor-locked)
- CustomEvent provides unlimited extensibility
- Easy to add new HPD events without breaking changes

✅ **Dual Protocol Support**
- AGUI for full features (default)
- Microsoft.Agents.AI for compatibility (opt-in)
- Users choose based on needs

### Trade-offs

⚠️ **Client SDK Complexity**
- Need TypeScript client SDK for full experience
- Standard AGUI clients work but miss HPD features
- **Mitigation**: Provide easy-to-use wrapper (`HPDAgent`)

⚠️ **CustomEvent Overhead**
- Extra JSON serialization for HPD events
- Slightly larger payloads
- **Impact**: Negligible (<5% overhead)

⚠️ **Documentation Burden**
- Need to document both AGUI and HPD layers
- More examples required
- **Mitigation**: Clear migration guides, comprehensive docs

---

## 6. Success Metrics

### Adoption Metrics
- **Target**: 80% of new users choose AGUI over Microsoft adapter
- **Metric**: Track adapter selection in AgentBuilder

### Performance Metrics
- **Target**: <5% overhead vs direct Microsoft adapter
- **Metric**: Benchmark event serialization performance

### Developer Experience
- **Target**: <10 lines of code to set up HPD features
- **Metric**: Sample code length in getting started guide

### Compatibility
- **Target**: 100% AGUI ecosystem compatibility
- **Metric**: Test with CopilotKit, Pydantic AI integrations

---

## 7. Open Questions & Risks

### Questions

1. **Bidirectional Communication**: How do we send responses back to the agent (e.g., permission approved)?
   - **Options**: WebSocket, SSE with separate POST endpoint, gRPC
   - **Recommendation**: SSE + POST endpoint (most compatible)

2. **State Synchronization**: How do we keep client state in sync with agent state?
   - **Answer**: Use `StateSnapshotEvent` / `StateDeltaEvent` from AGUI

3. **TypeScript SDK Distribution**: NPM package or monorepo?
   - **Recommendation**: Separate NPM package for easier consumption

### Risks

⚠️ **AGUI Protocol Changes**
- **Risk**: AGUI spec evolves, breaks compatibility
- **Mitigation**: Pin AGUI SDK version, test upgrades carefully

⚠️ **CustomEvent Complexity**
- **Risk**: Developers misuse CustomEvent, create incompatible events
- **Mitigation**: Strict TypeScript types, validation, documentation

⚠️ **Maintenance Burden**
- **Risk**: Supporting two protocols increases complexity
- **Mitigation**: Generate Microsoft adapter from AGUI adapter, shared tests

---

## 8. Conclusion

Making AGUI the default protocol is the **strategic choice** for HPD-Agent:

1. ✅ **Lossless event streaming** preserves all HPD innovations
2. ✅ **Ecosystem compatibility** provides wide adoption path
3. ✅ **Future-proof** via emerging standard protocol
4. ✅ **Extensible** via CustomEvent mechanism (no fork needed)

The Microsoft.Agents.AI adapter remains available for users locked into that ecosystem, providing a pragmatic compatibility layer while encouraging migration to the superior AGUI experience.

**Recommendation**: Proceed with implementation starting Phase 1.

---

## Appendix A: Event Type Reference

### AGUI Native Events (Used Directly)

1. `RunStartedEvent` - Message turn started
2. `RunFinishedEvent` - Message turn finished
3. `RunErrorEvent` - Message turn error
4. `StepStartedEvent` - Agent turn started
5. `StepFinishedEvent` - Agent turn finished
6. `TextMessageStartEvent` - Text message started
7. `TextMessageContentEvent` - Text delta
8. `TextMessageEndEvent` - Text message ended
9. `ReasoningStartEvent` - Reasoning block started
10. `ReasoningMessageStartEvent` - Reasoning message started
11. `ReasoningMessageContentEvent` - Reasoning delta
12. `ReasoningMessageEndEvent` - Reasoning message ended
13. `ReasoningEndEvent` - Reasoning block ended
14. `ToolCallStartEvent` - Tool call started
15. `ToolCallArgsEvent` - Tool arguments streaming
16. `ToolCallEndEvent` - Tool call ended
17. `ToolCallResultEvent` - Tool result
18. `FunctionPermissionRequestEvent` - Permission request (HPD supported!)
19. `ContinuationPermissionRequestEvent` - Continuation request (HPD supported!)
20. `StateSnapshotEvent` - State snapshot
21. `StateDeltaEvent` - State delta

### HPD CustomEvents (Mapped via CustomEvent)

22. `CustomEvent(PermissionResponse)` - Permission response
23. `CustomEvent(PermissionApproved)` - Permission approved
24. `CustomEvent(PermissionDenied)` - Permission denied
25. `CustomEvent(ContinuationResponse)` - Continuation response
26. `CustomEvent(ClarificationRequest)` - Clarification request
27. `CustomEvent(ClarificationResponse)` - Clarification response
28. `CustomEvent(FilterProgress)` - Filter progress update
29. `CustomEvent(FilterError)` - Filter error

**Total**: 29 event types (21 native + 8 custom)

---

## Appendix B: TypeScript Type Definitions

```typescript
// CustomEvent type definitions for HPD events

export interface PermissionRequestData {
  permissionId: string;
  functionName: string;
  description: string;
  callId: string;
  arguments: Record<string, any>;
}

export interface PermissionResponseData {
  permissionId: string;
  approved: boolean;
  reason?: string;
  choice: 'Ask' | 'Allow' | 'Deny' | 'AllowSession' | 'DenySession';
}

export interface ClarificationRequestData {
  requestId: string;
  agentName: string;
  question: string;
  options?: string[];
}

export interface ClarificationResponseData {
  requestId: string;
  question: string;
  answer: string;
}

export interface FilterProgressData {
  sourceName: string;
  message: string;
  percentComplete: number;
}

export interface FilterErrorData {
  sourceName: string;
  errorMessage: string;
  exceptionType?: string;
}

export type HPDCustomEventData =
  | { name: 'PermissionRequest'; data: PermissionRequestData }
  | { name: 'PermissionResponse'; data: PermissionResponseData }
  | { name: 'PermissionApproved'; data: { permissionId: string } }
  | { name: 'PermissionDenied'; data: { permissionId: string; reason?: string } }
  | { name: 'ClarificationRequest'; data: ClarificationRequestData }
  | { name: 'ClarificationResponse'; data: ClarificationResponseData }
  | { name: 'FilterProgress'; data: FilterProgressData }
  | { name: 'FilterError'; data: FilterErrorData };
```

---

**End of Proposal**
