# Token Tracking - Implementation

**Status:** ✅ Implemented - Provider-reported counts only
**Created:** 2025-01-30
**Approach:** Simple, honest, no estimation

---

## What This Does

Captures and stores **actual token counts reported by LLM providers** after each API call. No estimation, no guessing.

---

## How It Works

### 1. **Provider Reports Usage**

After each streaming LLM call, providers send usage data in the final chunk:

```json
{
  "usage": {
    "prompt_tokens": 1504,
    "completion_tokens": 127,
    "total_tokens": 1631
  }
}
```

Microsoft.Extensions.AI parses this into `ChatResponse.Usage`:
```csharp
response.Usage.InputTokenCount = 1504  // Prompt tokens
response.Usage.OutputTokenCount = 127  // Completion tokens
```

### 2. **Agent Captures from Streaming**

In `Agent.cs`, the `ConstructChatResponseFromUpdates` method aggregates all streaming chunks and extracts the usage:

```csharp
private static ChatResponse ConstructChatResponseFromUpdates(List<ChatResponseUpdate> updates)
{
    UsageDetails? usage = null;

    foreach (var update in updates)
    {
        // ... other aggregation ...

        // Final chunk typically has usage totals
        if (update.Usage != null)
            usage = update.Usage;
    }

    return new ChatResponse(chatMessage)
    {
        Usage = usage  // Included in response
    };
}
```

### 3. **Tokens Stored in Messages**

The `CaptureTokenCounts` method stores provider counts in message metadata:

```csharp
private static void CaptureTokenCounts(ChatResponse response, ChatMessage assistantMessage)
{
    if (response.Usage == null) return;

    // Store output tokens (what the LLM generated)
    if (response.Usage.OutputTokenCount.HasValue)
    {
        assistantMessage.SetOutputTokens(response.Usage.OutputTokenCount.Value);
    }

    // Note: Input tokens represent cumulative context, not stored per-message
}
```

### 4. **Persisted in AdditionalProperties**

Tokens are stored in the message's `AdditionalProperties` dictionary:

```csharp
message.AdditionalProperties["OutputTokens"] = 127
```

This persists across conversation saves/loads since `ChatMessage` serialization includes `AdditionalProperties`.

---

## Usage

### Get Token Counts from Messages

```csharp
using HPD_Agent;

// Get output tokens (what the assistant generated)
int outputTokens = message.GetOutputTokens();  // 127 or 0 if not set

// Get total tokens for a message
int total = message.GetTotalTokens();  // Input + Output

// Calculate total for conversation
var messages = await conversation.Messages.LoadMessagesAsync();
int conversationTokens = messages.CalculateTotalTokens();
```

### Get Token Statistics

```csharp
var stats = await conversation.Messages.GetTokenStatisticsAsync();

Console.WriteLine($"Total messages: {stats.TotalMessages}");
Console.WriteLine($"Total tokens: {stats.TotalTokens}");
Console.WriteLine($"Messages after summary: {stats.MessagesAfterSummary}");
Console.WriteLine($"Tokens after summary: {stats.TokensAfterSummary}");
```

---

## What This Does NOT Do

❌ **No Estimation** - If the provider didn't report tokens, the count is 0
❌ **No Tokenization** - We don't run tiktoken or any tokenizer
❌ **No Pre-flight Counts** - Can't estimate tokens before sending
❌ **No Input Token Tracking** - Input tokens are cumulative context, not per-message

---

## Why This Approach?

### ✅ **Advantages**
1. **Provider-Accurate** - Uses their tokenizer, their rules
2. **Free** - Included in every API response
3. **Simple** - No dependencies, no estimation logic
4. **Cross-Provider** - Works with OpenAI, Anthropic, Google via M.E.AI
5. **Honest** - No misleading "estimated" counts

### ⚠️ **Limitations**
1. **Reactive Only** - Only know tokens AFTER the call
2. **No User Messages** - User messages don't get individual counts (part of input context)
3. **Zero for Missing** - Messages without provider data show 0 tokens

---

## When Do You Get Token Counts?

| Message Type | Has Token Count? | Why |
|--------------|------------------|-----|
| Assistant (after LLM call) | ✅ Yes | Provider reports output tokens |
| User | ❌ No | Part of input context (not tracked per-message) |
| System | ❌ No | Part of input context (not tracked per-message) |
| Tool Result | ❌ No | Part of input context (not tracked per-message) |

**Note:** Input tokens represent the ENTIRE context sent to the LLM (all previous messages + system prompt + tools), so they're not attributable to a single message.

---

## Files Modified

1. **`ChatMessageTokenExtensions.cs`** (NEW)
   - Simple helpers to get/set provider token counts
   - No estimation logic

2. **`Agent.cs`**
   - `ConstructChatResponseFromUpdates()` - Aggregates usage from streaming
   - `CaptureTokenCounts()` - Stores tokens in messages

3. **`ConversationMessageStore.cs`**
   - Updated doc comments to clarify "provider-only, no estimation"

---

## Future: If You Need Pre-Flight Estimation

If you need to estimate tokens BEFORE sending (for prompt engineering, context window checks), you would:

1. **Add SharpToken** (OpenAI tokenizer for C#)
   ```bash
   dotnet add package SharpToken
   ```

2. **Create estimator**
   ```csharp
   var encoding = GptEncoding.GetEncoding("cl100k_base"); // GPT-4
   int estimatedTokens = encoding.Encode(text).Count;
   ```

3. **Provider-specific** - Would need different tokenizers for Anthropic, Google, etc.

**Current decision:** Don't implement this unless actually needed. Provider counts are enough for post-hoc analysis and billing.

---

## Summary

**What we have:** Simple, honest token tracking that captures what providers report.

**What we don't have:** Estimation, pre-flight counting, or complicated tokenization logic.

**Why:** Because BAML doesn't solve it either - they just capture provider counts, which is what we do now.

**Result:** Clean, maintainable, accurate (when available) token tracking.
