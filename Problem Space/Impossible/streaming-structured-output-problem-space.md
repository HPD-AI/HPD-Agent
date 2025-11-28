# Streaming Structured Output: Problem Space Analysis

## Executive Summary

HPD-Agent is **streaming-first** by design. The event-driven architecture enables real-time display, interactive middleware (permissions, continuations), and flexible presentation. However, **structured outputs** (typed responses like `T` instead of `string`) traditionally require waiting for complete JSON before validation. This creates a fundamental tension between streaming and type safety.

---

## Problem 1: Streaming vs. Validation Timing

**Severity: ARCHITECTURAL**
**Status: Core design tension**

### The Core Issue

Structured output requires valid JSON to deserialize into `T`. But during streaming, JSON arrives incrementally and is invalid until complete.

**Example - Streaming JSON:**
```
Token 1: {"name": "Jo
Token 2: hn", "age": 3
Token 3: 0}
```

At Token 1, the JSON `{"name": "Jo` is **invalid** - it cannot be deserialized.

### What This Means

| Approach | Streaming Display | Type Safety | Interactive Middleware |
|----------|-------------------|-------------|------------------------|
| Wait for complete JSON | No | Yes | Blocked until end |
| Stream raw text, validate at end | Yes (text only) | Yes (delayed) | Works |
| Partial JSON validation | Yes | Partial | Works |

### The Tension

HPD-Agent's event-driven architecture requires the stream to flow for:
- `PermissionRequestEvent` - mid-execution user consent
- `ContinuationRequestEvent` - iteration limit prompts
- `TextDeltaEvent` - real-time display

If we block waiting for complete JSON, these features break.

---

## Problem 2: Partial JSON Validation Complexity

**Severity: HIGH**
**Status: No native .NET support**

### The Core Issue

Pydantic (Python) solves this with `allow_partial='trailing-strings'` - a feature that accepts incomplete JSON like `{"name": "Jo` and returns `{"name": "Jo"}`. This is implemented deep in their Rust JSON parser (jiter).

**.NET has no equivalent.**

### What Pydantic Does (Complex Multi-Layer System)

1. **jiter (Rust JSON parser)** - Has `PartialMode` enum: `Off`, `On`, `TrailingStrings`
2. **Lookahead Iterator** - `EnumerateLastPartial` tracks which element is last in a sequence
3. **Conditional Error Suppression** - Validation errors on the last element are silently ignored
4. **Parser-Level Integration** - `trailing-strings` mode handles unterminated strings in the parser itself

**This is NOT simple string manipulation.** It's ~15 Rust files with:
- Iterator state tracking (lookahead)
- Conditional error suppression per element position
- Recursive validation state propagation
- Multiple `PartialMode` variants

### .NET JSON Parsing Limitations

`System.Text.Json`:
- No partial mode
- Throws `JsonException` on incomplete JSON
- No way to "fix" incomplete JSON during parsing

`Newtonsoft.Json`:
- `JsonTextReader` can do incremental reading
- But still fails on incomplete tokens
- No "trailing-strings" equivalent

### Naive Workarounds (All Have Issues)

**Approach 1: Try-Catch + Bracket Counting**
```csharp
string TryFixIncompleteJson(string json)
{
    int openBraces = json.Count(c => c == '{') - json.Count(c => c == '}');
    return json + new string('}', openBraces);
}
```
**Problems:**
- Doesn't handle strings containing braces: `{"text": "hello { world"}`
- Doesn't handle unterminated strings
- Doesn't handle arrays properly
- Doesn't handle nested structures correctly

**Approach 2: Regex-Based Fixing**
**Problems:**
- JSON is not regular - regex cannot parse it correctly
- Edge cases explode combinatorially

**Approach 3: Build Custom Partial Parser**
**Problems:**
- Essentially reimplementing jiter in C#
- Months of work for correctness
- Maintenance burden

---

## Problem 3: Event Type Design for Structured Output

**Severity: MEDIUM**
**Status: API design question**

### The Core Issue

Current events are text-focused:
```csharp
public record TextDeltaEvent(string Text, string MessageId) : AgentEvent;
public record TextMessageEndEvent(string MessageId) : AgentEvent;
```

For structured output, we need to emit typed results. But when?

### Design Questions

1. **When to emit the typed result?**
   - On every delta (partial `T`)? Requires partial validation.
   - Only at end (complete `T`)? Loses streaming benefit for structured data.
   - Both? Complex event semantics.

2. **What about validation errors?**
   - If final JSON is invalid, what event do we emit?
   - How does the observer know it failed?

3. **How to request structured output?**
   - Generic overload: `RunAsync<T>(message, thread)`?
   - Options parameter: `RunAsync(message, thread, new OutputOptions<T>())`?
   - Config-level: `AgentConfig.OutputType = typeof(T)`?

4. **Multiple output modes?**
   - Native (OpenAI JSON mode, Anthropic JSON)
   - Tool-based (function call returns structured data)
   - Prompted (schema in system prompt, hope for the best)

---

## Problem 4: Provider Heterogeneity

**Severity: MEDIUM**
**Status: Provider-specific capabilities**

### The Core Issue

Different providers have different structured output support:

| Provider | Native JSON Mode | JSON Schema | Streaming + Structured |
|----------|------------------|-------------|------------------------|
| OpenAI | Yes (`response_format`) | Yes | Partial support |
| Anthropic | Yes (tool_choice) | Via tools | No native |
| Google | Yes | Yes | Unknown |
| Ollama | Model-dependent | No | No |
| Azure OpenAI | Yes | Yes | Partial |

### What This Means

- Cannot assume all providers support structured output
- Cannot assume streaming + structured works everywhere
- Need fallback strategies (prompted mode)
- Provider abstraction (`IChatClient`) doesn't expose these capabilities uniformly

---

## Problem 5: AOT Compatibility

**Severity: LOW-MEDIUM**
**Status: Constraint**

### The Core Issue

HPD-Agent targets Native AOT. Structured output typically requires:
- JSON schema generation from `T`
- Deserialization to `T`

Both require reflection unless using source generators.

### Constraints

- `JsonSerializer.Deserialize<T>` needs `JsonTypeInfo<T>` for AOT
- Schema generation from `T` needs compile-time processing
- Cannot use `typeof(T).GetProperties()` at runtime in AOT

---

## Problem 6: Interaction with Existing Features

**Severity: MEDIUM**
**Status: Integration complexity**

### Tool Calls vs. Structured Output

When the agent calls tools, it's already producing "structured output" (tool arguments). How does explicit structured output interact?

**Scenario:** User asks for structured data, but agent decides to call a tool first.
- Does the tool result become the structured output?
- Does the agent need another turn to produce the final structured response?
- What if the agent produces text + structured output?

### Reasoning Tokens

Models with reasoning (o1, Gemini-thinking) produce reasoning tokens before the response. How does structured output interact?

- Reasoning is text, response is structured JSON
- Need to separate `Reasoning` events from `StructuredOutputDelta` events
- When does the structured output "start"?

### History Reduction

If structured output is stored in conversation history, how does history reduction handle it?
- Compress the JSON?
- Keep full structured responses?
- Token counting for JSON vs. text?

---

## Current State: What We Have

### Microsoft.Extensions.AI Approach

```csharp
public class ChatResponse<T> : ChatResponse
{
    public T Result { get; }  // Lazy deserialization
    public bool TryGetResult(out T? result);
}
```

**Limitations:**
- Non-streaming only
- Deserializes from final response text
- No partial support

### PydanticAI Approach (Python Reference)

```python
async def stream_output(self) -> AsyncIterator[OutputDataT]:
    async for response in self.stream_responses():
        yield await self.validate_response_output(response, allow_partial=True)
```

**What They Have:**
- Partial validation via `allow_partial='trailing-strings'`
- Multiple output modes (native, tool, prompted, auto)
- Event-driven with `PartStartEvent`, `PartDeltaEvent`, `PartEndEvent`

---

## Summary of Problems

| Problem | Severity | Blocking? | Notes |
|---------|----------|-----------|-------|
| Streaming vs. validation timing | Architectural | Yes | Core tension |
| No partial JSON in .NET | High | Yes | Would need custom parser |
| Event type design | Medium | No | API design work |
| Provider heterogeneity | Medium | No | Need abstraction |
| AOT compatibility | Low-Medium | No | Source generators exist |
| Feature interaction | Medium | No | Integration complexity |

---

## Open Questions

1. **Is partial streaming of structured output actually needed?**
   - Or is "stream text for display, validate at end" sufficient?

2. **Should structured output be a separate API?**
   - `RunAsync<T>()` vs `RunStructuredAsync<T>()`?

3. **How much provider abstraction is needed?**
   - Should we detect provider capabilities and auto-select mode?

4. **Is building a partial JSON parser worth it?**
   - Significant effort, ongoing maintenance
   - May only benefit edge cases

5. **What's the MVP?**
   - Stream raw, validate at end?
   - Native mode only (OpenAI/Anthropic)?
   - Full PydanticAI-style partial streaming?
