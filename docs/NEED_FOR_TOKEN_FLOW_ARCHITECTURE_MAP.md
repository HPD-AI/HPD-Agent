# The Case for a Token Flow Architecture Map

**Date**: 2025-01-31
**Status**: Critical Infrastructure Need

---

## The Realization

After investigating token tracking for history reduction, we discovered that tokens flow through the system in **far more complex ways than initially understood**. What started as "add token tracking" has revealed a labyrinth of token sources, transformations, and hidden costs.

**The scary realization**: We don't fully understand where all the tokens go, and **neither do our users**.

---

## Why This Matters: The Cost Creep Problem

### Real-World Impact

**Scenario: A startup using HPD-Agent for customer support**

```
Configuration:
  - RAG-based agent with vector DB
  - Memory system with conversation history
  - System prompt: 500 tokens
  - Average documents per query: 15KB (4,000 tokens)
  - Conversation grows to 100 turns

What they THINK they're spending:
  100 turns × 200 tokens/turn = 20,000 tokens total
  Cost estimate: $0.60 (at $0.03/1k tokens)

What they're ACTUALLY spending PER TURN:
  - System prompt: 500 tokens
  - RAG documents: 4,000 tokens
  - Memory context: 1,000 tokens (injected by prompt filter)
  - Conversation history: 200 tokens (tracked)
  Total: 5,700 tokens per turn

  100 turns × 5,700 = 570,000 tokens
  Actual cost: $17.10

❌ Off by 28.5x!
```

**This is why companies complain about unexpected LLM costs.**

---

## What We've Discovered So Far

### Token Sources We Know About

1. **User messages** (passed in by user)
   - Tracking: ❌ Not tracked
   - In turnHistory: ❌ No (user already has them)
   - In currentMessages: ✅ Yes
   - Sent to LLM: ✅ Yes

2. **System instructions** (added by PrependSystemInstructions)
   - Tracking: ❌ Not tracked
   - In turnHistory: ❌ No
   - In currentMessages: ✅ Yes
   - Sent to LLM: ✅ Yes

3. **Prompt filter injections** (documents, RAG, memory)
   - Tracking: ❌ Not tracked
   - In turnHistory: ❌ No
   - In currentMessages: ✅ Yes
   - Sent to LLM: ✅ Yes

4. **Summary messages** (from history reduction)
   - Tracking: ❓ Unknown (need to check)
   - In turnHistory: ❓ Unclear
   - In currentMessages: ✅ Yes
   - Sent to LLM: ✅ Yes

5. **Previous assistant messages** (from past turns)
   - Tracking: ✅ Output tokens tracked
   - In turnHistory: ✅ Yes (for new messages)
   - In currentMessages: ✅ Yes
   - Sent to LLM: ✅ Yes

6. **Tool messages** (function results)
   - Tracking: ✅ NOW estimated (we just added this)
   - In turnHistory: ✅ Yes
   - In currentMessages: ✅ Yes (filtered, no containers)
   - Sent to LLM: ✅ Yes

7. **Container expansion messages** (plugin/skill lists)
   - Tracking: ✅ Correctly 0 (ephemeral)
   - In turnHistory: ✅ Yes (for visibility)
   - In currentMessages: ❌ No (filtered out)
   - Sent to LLM: ❌ No

8. **Reasoning content** (extended thinking)
   - Tracking: ❓ Unknown
   - In turnHistory: ❓ Check TextReasoningContent handling
   - In currentMessages: ❓ Unknown
   - Sent to LLM: ❓ Unknown

9. **AdditionalProperties on messages** (metadata)
   - Tracking: ❌ Not tracked
   - In turnHistory: ✅ Yes (preserved)
   - In currentMessages: ✅ Yes
   - Sent to LLM: ❓ Does M.E.AI serialize these?

10. **ChatOptions.AdditionalProperties** (request metadata)
    - Tracking: ❌ Not tracked
    - Sent to LLM: ❓ Unknown how providers handle this

### Token Transformations We Know About

1. **PrependSystemInstructions** (Agent.cs)
   - Adds system prompt to message list
   - Token impact: +500-2000 tokens typical

2. **History Reduction** (PrepareMessagesAsync)
   - Removes old messages
   - Adds summary message
   - Token impact: Reduces by 50-90%, adds 200-500 for summary

3. **Prompt Filters** (ApplyPromptFiltersAsync)
   - Can add messages (documents, memory)
   - Can modify messages (content transformation)
   - Can remove messages (filtering)
   - Token impact: Highly variable, +0 to +50,000 tokens

4. **Tool Result Filtering** (Agent.cs:871-905)
   - Filters out container expansions from currentMessages
   - Keeps them in turnHistory
   - Token impact: Reduces persistent history, but what about this turn?

5. **Reasoning Content Filtering** (Agent.cs:808)
   - Strips TextReasoningContent from history
   - Token impact: Unknown - how much is reasoning typically?

### Questions We Still Have

1. **Does reasoning content go to the LLM in the current turn?**
   - We strip it from history (line 808)
   - But what about during the streaming turn when it's generated?

2. **What happens to tool messages in the same turn?**
   - Assistant calls tool A
   - Tool A returns result (5000 tokens)
   - Same turn, assistant calls tool B
   - Does tool A result get sent to LLM for tool B call?
   - YES (line 904: added to currentMessages)

3. **What's the token impact of multimodal content?**
   - Images in messages
   - Audio content
   - How do providers count these?

4. **What happens with caching?**
   - Anthropic prompt caching
   - OpenAI ephemeral caching
   - Do cached tokens count toward limits?
   - We track cache_read_input_tokens but don't use it

5. **What about ConversationId optimization?**
   - Line 636: When innerClientTracksHistory, we only send NEW messages
   - This affects token counts!
   - Are we tracking this correctly?

6. **What's in effectiveOptions.AdditionalProperties?**
   - Can prompt filters add stuff here?
   - Does it affect token counts?

---

## Why We Need the Map NOW

### Problem 1: Incomplete Token Tracking

We've added tracking for:
- ✅ Assistant output tokens
- ✅ Tool message tokens (estimated)

But we're missing:
- ❌ System instructions
- ❌ Prompt filter injections
- ❌ User messages
- ❌ Reasoning content
- ❌ Summary messages
- ❌ Cached tokens impact

**Impact**: History reduction triggers too late, costs surprise users.

---

### Problem 2: No Single Source of Truth

Information is scattered across:
- Agent.cs (multiple sections)
- MessageProcessor
- Prompt filter implementations
- Tool execution logic
- Container expansion logic
- M.E.AI abstractions

**Impact**: Impossible to reason about total token flow, can't optimize costs.

---

### Problem 3: Hidden Accumulation Paths

**Example we just discovered**:

```
Turn 1:
  currentMessages = [System, User]

Turn 2:
  currentMessages = [System, User1, Assistant1, Tool1, User2]

Turn 3:
  currentMessages = [System, User1, Assistant1, Tool1, User2, Assistant2, Tool2, User3]
```

**Tool messages accumulate in currentMessages!**

Even though we filter container expansions (line 904), regular tool results DO accumulate. This means:
- Turn 10: 10 tool results in history
- Turn 20: 20 tool results in history
- If each is 5KB, that's 100KB+ by turn 20!

**But**: We only estimate tokens ONCE when created. If the user doesn't pass back full history, we lose the token counts!

---

### Problem 4: User Can Break Token Tracking

**User workflow**:
```csharp
// Turn 1
var result1 = await agent.RunAsync([userMsg1]);
var history1 = await result1.GetHistoryAsync();

// Turn 2 - User modifies history
var modifiedHistory = history1
    .Where(m => m.Role != ChatRole.Tool)  // Remove tool messages
    .ToList();

await agent.RunAsync([...modifiedHistory, userMsg2]);
```

**What happens**:
1. Tool messages had estimated token counts
2. User filtered them out
3. CalculateTotalTokens() now undercounts
4. But those tool messages were in the LLM context!

**Impact**: Token tracking diverges from reality.

---

### Problem 5: Can't Validate Accuracy

**We have no way to check**:
- Is our estimation accurate?
- Are we missing token sources?
- Do our thresholds make sense?

**Because we don't have**:
- A complete map of token flow
- A way to compare estimated vs actual
- Telemetry on token accuracy

---

## What The Map Would Give Us

### 1. Complete Token Flow Diagram

```
User Input
  ↓
┌─────────────────────────────────────┐
│ PrepareMessagesAsync                │
│                                     │
│  ┌─ PrependSystemInstructions      │
│  │   └─ +500-2000 tokens           │
│  │                                  │
│  ┌─ History Reduction              │
│  │   ├─ Removes: -N messages       │
│  │   └─ Adds: +200-500 tokens      │
│  │                                  │
│  └─ ApplyPromptFiltersAsync        │
│      ├─ RAG injection: +0-50k      │
│      ├─ Memory injection: +0-10k   │
│      └─ Document injection: +0-30k │
│                                     │
│  = effectiveMessages                │
└─────────────────────────────────────┘
  ↓
┌─────────────────────────────────────┐
│ Agentic Loop                        │
│                                     │
│  Iteration 1:                       │
│    ├─ LLM Call                      │
│    │   Input: effectiveMessages    │
│    │   Output: Assistant + Tools   │
│    │   Tokens: Input=X, Output=Y   │
│    │                                │
│    ├─ Tool Execution                │
│    │   Returns: Tool messages      │
│    │   Estimated tokens: Z         │
│    │                                │
│    └─ Add to currentMessages        │
│        currentMessages += [Asst, Tool]
│                                     │
│  Iteration 2:                       │
│    ├─ LLM Call                      │
│    │   Input: currentMessages       │
│    │   (includes Iteration 1 results)
│    │   Output: Assistant response  │
│    │   Tokens: Input=X+Z, Output=W │
│                                     │
│    └─ Break (no more tools)        │
│                                     │
└─────────────────────────────────────┘
  ↓
Return turnHistory
  (Only new Asst + Tool messages)
```

### 2. Token Accounting Table

| Source | Created | In currentMessages | In turnHistory | Sent to LLM | Tracked | Persists |
|--------|---------|-------------------|----------------|-------------|---------|----------|
| User messages (input) | Input | ✅ | ❌ | ✅ | ❌ | Via user |
| System instructions | PrependSys | ✅ | ❌ | ✅ | ❌ | No |
| History summary | Reduction | ✅ | ❌ | ✅ | ❓ | Via user if returned |
| RAG documents | PromptFilter | ✅ | ❌ | ✅ | ❌ | No |
| Memory context | PromptFilter | ✅ | ❌ | ✅ | ❌ | No |
| Previous assistant | Input | ✅ | ❌ | ✅ | ✅ | Via user |
| Previous tools | Input | ✅ | ❌ | ✅ | ✅ | Via user |
| NEW assistant | AgenticLoop | ✅ | ✅ | ✅ | ✅ | Yes |
| NEW tools | AgenticLoop | ✅ | ✅ | ✅ | ✅ | Yes |
| Container expansions | ToolExec | ❌ | ✅ | ❌ | ✅ (0) | No |
| Reasoning content | LLM | ✅? | ❌ | ✅? | ❌ | No |

### 3. Cost Attribution Model

**Per-Turn Cost Breakdown**:
```
Turn N cost =
  System tokens (constant)
  + Ephemeral context tokens (variable per turn)
  + Accumulated history tokens (grows over time)
  + New response tokens (variable per turn)

Example:
  System: 500 tokens
  RAG docs: 4,000 tokens
  Memory: 1,000 tokens
  History (turns 1-N): 200N tokens
  Response: 150 tokens

Turn 1:  500 + 4000 + 1000 + 200   + 150 = 5,850 tokens
Turn 10: 500 + 4000 + 1000 + 2,000 + 150 = 7,650 tokens
Turn 50: 500 + 4000 + 1000 + 10,000 + 150 = 15,650 tokens

Cost trend:
  - Starts high (ephemeral context)
  - Grows linearly (history accumulation)
  - History reduction at turn X drops to baseline
```

### 4. Validation Strategy

**Comparing Estimated vs Actual**:
```csharp
public class TokenAccuracyMetrics
{
    public int TurnNumber { get; set; }

    // What we estimated
    public int EstimatedTotal { get; set; }
    public int EstimatedHistory { get; set; }
    public int EstimatedEphemeral { get; set; }

    // What API reported
    public int ActualInputTokens { get; set; }
    public int ActualOutputTokens { get; set; }

    // Accuracy
    public double InputAccuracy =>
        (double)EstimatedTotal / ActualInputTokens;

    public int ErrorMargin =>
        Math.Abs(EstimatedTotal - ActualInputTokens);
}
```

**Telemetry over time**:
- Track accuracy per turn
- Identify systematic biases
- Tune estimation ratios
- Warn users about high costs

---

## Proposed Approach

### Phase 1: Map the Current State (This Document)

**Goal**: Understand EXACTLY where tokens flow

**Tasks**:
1. ✅ Document known token sources (done above)
2. ⏳ Trace every code path that creates/modifies messages
3. ⏳ Document every transformation point
4. ⏳ Create visual flow diagram
5. ⏳ Build token accounting table
6. ⏳ Identify all unknowns

**Deliverable**: Complete token flow architecture document

**Effort**: 1-2 days of deep investigation

**Value**:
- Prevents future bugs
- Enables accurate cost estimation
- Foundation for optimization
- Reference for stakeholders

---

### Phase 2: Fill the Gaps

**Goal**: Answer all the unknowns

**Tasks**:
1. Test reasoning content behavior
2. Test multimodal token counting
3. Test ConversationId optimization impact
4. Test prompt filter injection patterns
5. Test cache token handling
6. Document findings

**Deliverable**: Updated architecture map with no unknowns

**Effort**: 3-4 hours of testing

---

### Phase 3: Implement Complete Tracking

**Goal**: Track ALL token sources accurately

**Based on Phase 1 findings, implement**:
1. Last API input token tracking (Option A from INPUT_TOKEN_TRACKING_ANALYSIS)
2. Ephemeral context estimation
3. Telemetry and validation
4. User-facing cost estimates

**Deliverable**: Production-ready token tracking

**Effort**: 1-2 days of implementation

---

### Phase 4: Optimization

**Goal**: Reduce costs without sacrificing quality

**Based on complete understanding**:
1. Identify high-cost paths
2. Add caching where beneficial
3. Optimize prompt filter efficiency
4. Implement smart context pruning
5. User guidance on cost optimization

**Deliverable**: Cost optimization guide + features

**Effort**: Ongoing

---

## Why This Matters More Than We Thought

### The Compounding Effect

**Early in conversation**:
- Ephemeral context dominates (80% of tokens)
- History is small (20%)

**After 50+ turns**:
- Ephemeral context: still 5-6k tokens
- History: now 10-20k tokens
- Tool results: 5-10k tokens
- **Total: 20-36k tokens per turn**

**Without history reduction**: Context fills up fast
**With broken history reduction**: Triggers too late, costs explode

---

### The Trust Factor

**Users trust the framework** to:
1. Track tokens accurately
2. Trigger reduction appropriately
3. Keep costs predictable

**If we get this wrong**:
- ❌ Users get surprise bills
- ❌ Reduction doesn't trigger when needed
- ❌ Context overflow errors
- ❌ Loss of trust in the framework

---

### The Competitive Advantage

**If we get this RIGHT**:
- ✅ Most accurate token tracking in the market
- ✅ Proactive cost warnings
- ✅ Intelligent reduction triggers
- ✅ Cost optimization features
- ✅ Telemetry and insights
- ✅ Trust and reliability

**No other framework has this**:
- LangChain: No token tracking
- Semantic Kernel: Basic tracking only
- AutoGen: Message-count only

**We could be THE framework for production LLM apps** by solving the cost problem.

---

## Conclusion

**The bottom line**: We don't fully understand where all the tokens go, and this is a **critical infrastructure gap**.

**The cost creep problem is real**: Companies complain because hidden token sources accumulate without visibility.

**The solution**: Before implementing more patchwork fixes, we need a **complete Token Flow Architecture Map**.

**The payoff**:
1. Accurate token tracking
2. Predictable costs
3. Competitive advantage
4. Production readiness
5. User trust

**The effort**: 2-3 days of deep investigation and documentation

**The priority**: **HIGH** - This affects every user, every conversation, every dollar spent.

---

## Next Steps

1. **Get buy-in**: Does this level of investigation make sense?
2. **Allocate time**: This is infrastructure work, not a feature
3. **Deep dive**: Trace every code path methodically
4. **Document**: Create the definitive token flow reference
5. **Implement**: Build complete tracking based on complete understanding
6. **Validate**: Measure accuracy and tune
7. **Optimize**: Now we can optimize from a position of knowledge

**Decision needed**: Proceed with comprehensive token flow mapping?
