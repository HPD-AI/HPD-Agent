# Input Token Tracking Analysis

**Date**: 2025-01-31
**Question**: Should we track input tokens? What affects input tokens that isn't in turnHistory?

---

## The Flow: What Goes Where

### PrepareMessagesAsync (Before LLM Call)

```
User provides: messages (from previous turns)
    ↓
PrependSystemInstructions(messages)
    ↓ Adds: System prompt
effectiveMessages = [System, ...messages]
    ↓
History Reduction (if enabled)
    ↓ May add: Summary message
    ↓ May remove: Old messages
effectiveMessages = [System, Summary?, ...recentMessages]
    ↓
ApplyPromptMiddlewaresAsync(effectiveMessages)
    ↓ May add: Document context, RAG results, injected instructions
    ↓ May modify: Existing messages
effectiveMessages = [System, Documents?, ...filteredMessages]
    ↓
Return effectiveMessages
```

### What Gets Sent to LLM

```csharp
// Agent.cs:506
var currentMessages = effectiveMessages.ToList();

// Later sent to LLM (Agent.cs:644)
messagesToSend = currentMessages;
```

**Contents of `currentMessages` (sent to LLM)**:
1. System instructions
2. Summary message (if history was reduced)
3. Document context (if prompt filters injected it)
4. User messages (from input)
5. Previous assistant messages (from input)
6. Previous tool messages (from input)
7. NEW assistant messages (generated during agentic loop)
8. NEW tool messages (generated during agentic loop)

### What Gets Returned to User

```csharp
// Agent.cs:1099
historyCompletionSource.TrySetResult(turnHistory);
```

**Contents of `turnHistory` (returned to user)**:
1. NEW assistant messages (from THIS turn)
2. NEW tool messages (from THIS turn)

**NOT included in turnHistory**:
- ❌ System instructions
- ❌ Summary messages
- ❌ Document context from prompt filters
- ❌ Previous messages (user already has them)

---

## The Problem: What's NOT Tracked

### Current Token Tracking

**What we track**:
- ✅ Assistant output tokens (provider-reported, accurate)
- ✅ Tool message tokens (character-based estimation, ~±20%)

**What we DON'T track**:
- ❌ System instructions tokens
- ❌ Document context tokens (from prompt filters)
- ❌ User message tokens
- ❌ Summary message tokens

### Impact on History Reduction

**Scenario: Agent with document context**

```
Turn 1:
User: "Summarize the codebase"

Prompt filter injects: [50KB of code from 20 files] = ~15,000 tokens

Messages sent to LLM:
  - System: "You are a code expert" = 50 tokens
  - Documents: [50KB of code] = 15,000 tokens  ← NOT in turnHistory
  - User: "Summarize the codebase" = 10 tokens
  Total input: 15,060 tokens

LLM generates:
  - Assistant: "Here's the summary..." = 200 tokens

turnHistory returned:
  - Assistant: 200 tokens tracked ✅

Turn 2:
User passes back: [Assistant from Turn 1, "Tell me more"]
  Tracked: 200 tokens

PrepareMessagesAsync:
  CalculateTotalTokens() = 200 tokens
  Threshold: 90,000 tokens
  Should reduce? NO (200 < 90,000)

BUT: Prompt filter injects documents AGAIN!

Messages sent to LLM:
  - System: 50 tokens
  - Documents: 15,000 tokens  ← EPHEMERAL, not tracked
  - Assistant: 200 tokens
  - User: "Tell me more" = 10 tokens
  Total input: 15,260 tokens
```

**The issue**: Document context is injected by prompt filters on EVERY turn, but isn't tracked in history because it's not returned to the user.

---

## Two Categories of Input Tokens

### Category 1: Persistent (In turnHistory)

**These accumulate across turns**:
- Previous assistant messages
- Previous tool messages
- Summary messages (if reduction occurred)

**Tracking**: We DO track these (output + estimated tool tokens)

**For history reduction**: These are what `CalculateTotalTokens()` counts

---

### Category 2: Ephemeral (NOT in turnHistory)

**These are added fresh on each turn**:
- System instructions (added by `PrependSystemInstructions`)
- Document context (added by prompt filters)
- RAG results (added by prompt filters)
- Memory context (added by prompt filters)

**Tracking**: We DON'T track these

**For history reduction**: These are NOT counted by `CalculateTotalTokens()`

**Why they're ephemeral**:
- Generated fresh on each turn
- Not returned to user
- User doesn't pass them back
- Filters re-inject them automatically

---

## The Question: Should We Track Ephemeral Tokens?

### Argument FOR Tracking

**Problem**: History reduction decisions are inaccurate

**Example**:
```
Configuration:
  ContextWindowSize = 128,000
  TriggerPercentage = 0.7 (trigger at 89,600 tokens)

Scenario: RAG-based agent with document injection

Turn 10:
  turnHistory: 10,000 tokens tracked
  But ACTUALLY sent to LLM:
    - System: 500 tokens
    - Documents: 20,000 tokens (RAG results)
    - turnHistory: 10,000 tokens
    - New user message: 100 tokens
    Total: 30,600 tokens

  CalculateTotalTokens() = 10,000
  Should reduce? NO (10,000 < 89,600)

  Reality: Using 30,600 tokens per call

Turn 20:
  turnHistory: 30,000 tokens tracked
  Actually sent:
    - System: 500 tokens
    - Documents: 20,000 tokens
    - turnHistory: 30,000 tokens
    - New user message: 100 tokens
    Total: 50,600 tokens

  CalculateTotalTokens() = 30,000
  Should reduce? NO (30,000 < 89,600)

Turn 30:
  turnHistory: 60,000 tokens tracked
  Actually sent:
    - System: 500 tokens
    - Documents: 20,000 tokens
    - turnHistory: 60,000 tokens
    - New user message: 100 tokens
    Total: 80,600 tokens

  CalculateTotalTokens() = 60,000
  Should reduce? NO (60,000 < 89,600)

Turn 35:
  turnHistory: 80,000 tokens tracked
  Actually sent:
    - System: 500 tokens
    - Documents: 20,000 tokens
    - turnHistory: 80,000 tokens
    - New user message: 100 tokens
    Total: 100,600 tokens

  CalculateTotalTokens() = 80,000
  Should reduce? NO (80,000 < 89,600)

  BUT: Context window = 128,000
  Actual usage: 100,600 tokens
  Buffer left: 27,400 tokens

  ❌ Should have triggered reduction earlier!
```

**Impact**: For agents with significant ephemeral context (RAG, memory, docs), reduction triggers too late or not at all.

---

### Argument AGAINST Tracking

**Counterpoint**: Ephemeral tokens are constant overhead

**Rationale**:
1. System instructions: Same on every turn (~constant)
2. Document context: Relatively stable per query type
3. They don't ACCUMULATE like turnHistory does

**Example**:
```
If documents = 20,000 tokens per turn (constant):

Turn 1:  turnHistory=1,000  + docs=20,000 = 21,000 total
Turn 10: turnHistory=10,000 + docs=20,000 = 30,000 total
Turn 20: turnHistory=30,000 + docs=20,000 = 50,000 total

The GROWTH rate is determined by turnHistory, not docs.

So: Tracking turnHistory is sufficient to predict when to reduce.
```

**But wait**: This only works if:
- Document size is truly constant (not always true)
- We account for the constant offset in threshold calculations

---

## How Other Frameworks Handle This

### Gemini CLI

**They DON'T track ephemeral context**

```typescript
// They track the last API call's total input tokens
const originalTokenCount = uiTelemetryService.getLastPromptTokenCount();

// But when deciding compression threshold:
if (originalTokenCount < threshold * tokenLimit(model)) {
  return { compressionStatus: CompressionStatus.NOOP };
}
```

**Key insight**: They use the ACTUAL input token count from the LAST API call, which includes everything (system, documents, history).

**But**: They use this for TRIGGERING compression, not for calculating what to compress. They still estimate character counts for the compressed result.

---

### Codex (Claude Code)

**They DON'T track at all**

```rust
// Trial and error approach
loop {
    match try_api_call(messages) {
        Ok(_) => break,
        Err(ContextLengthExceeded) => {
            messages.remove_first_item();
            continue;
        }
    }
}
```

**No prediction needed** - the API tells them when it's too big.

---

## Proposed Solutions

### Option A: Track Last API Input Token Count

**What**: Store the total input tokens from the LAST LLM call

**Implementation**:
```csharp
// In CaptureTokenCounts
private static void CaptureTokenCounts(ChatResponse response, ChatMessage assistantMessage)
{
    if (response.Usage == null) return;

    // Store output tokens
    assistantMessage.SetOutputTokens((int)response.Usage.OutputTokenCount.Value);

    // ALSO store the total input tokens from this call
    if (response.Usage.InputTokenCount.HasValue)
    {
        // Store on the assistant message as metadata
        assistantMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        assistantMessage.AdditionalProperties["LastInputTokenCount"] =
            (int)response.Usage.InputTokenCount.Value;
    }
}

// In ShouldTriggerReduction
private bool ShouldReduceByPercentage(List<ChatMessage> messagesList, int lastSummaryIndex)
{
    // Get the LAST input token count (includes ephemeral context)
    var lastInputTokens = messagesList
        .LastOrDefault(m => m.AdditionalProperties?.ContainsKey("LastInputTokenCount") == true)
        ?.AdditionalProperties?["LastInputTokenCount"] as int? ?? 0;

    // Get tracked tokens (turnHistory only)
    var trackedTokens = messagesList.CalculateTotalTokens();

    // Estimate current total = last input tokens + growth since then
    var latestMessage = messagesList.Last();
    var lastTrackedMessage = messagesList.LastOrDefault(m =>
        m.AdditionalProperties?.ContainsKey("LastInputTokenCount") == true);

    int growthSinceLastCall = 0;
    if (lastTrackedMessage != null)
    {
        var indexOfLast = messagesList.IndexOf(lastTrackedMessage);
        var messagesSince = messagesList.Skip(indexOfLast + 1);
        growthSinceLastCall = messagesSince.Sum(m => m.GetTotalTokens());
    }

    var estimatedTotal = lastInputTokens + growthSinceLastCall;

    var threshold = (int)(contextWindow * triggerPercentage);
    return estimatedTotal > threshold;
}
```

**Pros**:
- ✅ Accounts for ALL tokens (system, documents, history)
- ✅ Uses provider-reported counts (accurate)
- ✅ Handles ephemeral context automatically
- ✅ Gemini CLI-inspired (validated approach)

**Cons**:
- ⚠️ Assumes ephemeral context is similar turn-to-turn
- ⚠️ Only accurate if document size doesn't change dramatically
- ⚠️ Requires storing extra metadata

---

### Option B: Estimate Ephemeral Context Size

**What**: Count tokens in system + prompt-filtered messages

**Implementation**:
```csharp
// In PrepareMessagesAsync, AFTER filters
private int EstimateEphemeralTokens(IEnumerable<ChatMessage> effectiveMessages,
                                     IEnumerable<ChatMessage> originalMessages)
{
    // Find messages that were ADDED by filters (not in original)
    var added = effectiveMessages.Except(originalMessages);

    int ephemeralTokens = 0;

    foreach (var msg in added)
    {
        // Estimate using character count
        int charCount = msg.Contents.Sum(c => c.ToString().Length);
        ephemeralTokens += (int)(charCount / 3.5);
    }

    // Add system instruction tokens (also ephemeral from user's perspective)
    var systemMsg = effectiveMessages.FirstOrDefault(m => m.Role == ChatRole.System);
    if (systemMsg != null)
    {
        int sysCharCount = systemMsg.Contents.Sum(c => c.ToString().Length);
        ephemeralTokens += (int)(sysCharCount / 3.5);
    }

    return ephemeralTokens;
}

// Store for use in reduction logic
context.Properties["EphemeralTokens"] = ephemeralTokens;
```

**Pros**:
- ✅ Proactive (don't need to wait for API call)
- ✅ Can warn user before context overflow

**Cons**:
- ❌ Estimation inaccuracy (±20%)
- ❌ Doesn't capture all sources (what about options.AdditionalProperties?)
- ❌ Complex to detect what was "added" vs "original"

---

### Option C: Don't Track, Adjust Thresholds

**What**: Document that thresholds should account for ephemeral context

**Implementation**:
```csharp
/// <summary>
/// When set, uses percentage-based triggers instead of absolute token counts.
/// Requires ContextWindowSize to be configured.
/// Example: 0.7 = trigger reduction at 70% of context window.
///
/// IMPORTANT: This percentage is calculated against TRACKED tokens only
/// (assistant outputs + tool results). Ephemeral context (system instructions,
/// document injections from prompt filters) is NOT tracked.
///
/// Recommended thresholds:
/// - No prompt filters: 0.7-0.8 (70-80%)
/// - With document injection: 0.4-0.5 (40-50%)
/// - Heavy RAG usage: 0.3-0.4 (30-40%)
///
/// Example: If your system prompt + documents = 20k tokens constant,
/// and context window = 128k, set threshold to 0.5 to trigger at 64k tracked tokens,
/// which accounts for the 20k ephemeral overhead (total ~84k actual).
/// </summary>
public double? TokenBudgetTriggerPercentage { get; set; } = null;
```

**Pros**:
- ✅ Simple - no code changes
- ✅ Puts control in user's hands
- ✅ Transparent about limitations

**Cons**:
- ❌ User must manually tune thresholds
- ❌ Fragile - breaks if ephemeral context changes
- ❌ Poor user experience

---

## Recommendation

**Implement Option A: Track Last API Input Token Count**

**Why**:
1. ✅ Most accurate - uses provider-reported numbers
2. ✅ Automatically accounts for ephemeral context
3. ✅ Validated by Gemini CLI (they do this)
4. ✅ Relatively simple implementation
5. ✅ No manual tuning required from users

**Implementation Priority**: Phase 2 (after tool token estimation)

**Alternative**: Combine Option A + Option C
- Implement Option A for accuracy
- Document Option C as guidance for tuning

**Don't implement**: Option B (too complex, still has estimation errors)

---

## Summary

### Current State

**Tracked**:
- Assistant output tokens (accurate)
- Tool message tokens (estimated ~±20%)

**NOT Tracked**:
- System instructions
- Document context from prompt filters
- User messages
- Ephemeral context in general

### Impact

**For text-only agents**: Current tracking is ~90% accurate

**For RAG/document-heavy agents**: Current tracking can be 50-80% accurate depending on document size

### Solution

**Phase 1 (Done)**: Tool token estimation
**Phase 2 (Recommended)**: Store last API input token count
**Phase 3 (Optional)**: Estimate ephemeral context proactively

This phased approach progressively improves accuracy without over-engineering.
