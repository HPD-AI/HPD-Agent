# FFI Filter System Proposal

## Executive Summary

We can enable **Rust/Python/JavaScript filters** using our **existing plugin FFI architecture**. No new patterns needed - just clone what we already built for plugins.

**Current Status:**
- ✅ Plugins work via FFI (Rust → C# → LLM)
- ❌ Filters are C#-only (no FFI support)

**Proposed:**
- ✅ Apply the same callback pattern to filters
- ✅ Rust users can create custom filters with `#[hpd_filter]` macro
- ✅ State flows through JSON (serializable, language-agnostic)

---

## Why This Matters

**Users want custom filters from external languages:**

```rust
// Rust filter (currently impossible)
#[hpd_filter("RequestCounter", "Counts requests and adds context")]
impl RequestCounter {
    #[filter_pre_invoke("Add request count to context")]
    pub async fn pre_invoke(&mut self, context: FilterContext) -> FilterResult {
        context.messages.insert(0, Message {
            role: "system",
            content: format!("This is request #{}", self.count)
        });

        self.count += 1;

        FilterResult {
            messages: context.messages,
            new_state: serde_json::to_string(&self).unwrap()
        }
    }

    #[filter_post_invoke("Extract insights from response")]
    pub async fn post_invoke(&mut self, context: PostInvokeContext) -> PostFilterResult {
        for msg in &context.response_messages {
            if let Some(insight) = extract_insight(&msg.content) {
                self.insights.push(insight);
            }
        }

        PostFilterResult {
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}
```

---

## Architecture Comparison

### Current Plugin System (Working)

```
Rust Plugin (Macro) → Register Executor → C# FFI Call → Execute Rust Function → Return JSON
```

**Code Flow:**
```rust
// 1. Define plugin
#[hpd_plugin("Math", "Math operations")]
impl MathPlugin {
    #[ai_function("Add two numbers")]
    pub async fn add(a: f64, b: f64) -> f64 { a + b }
}

// 2. Register executor (auto-generated)
register_async_executor("add", Arc::new(|args_json| {
    Box::pin(async move {
        let args: HashMap<String, Value> = serde_json::from_str(&args_json)?;
        let a = args["a"].as_f64().unwrap();
        let b = args["b"].as_f64().unwrap();
        let result = MathPlugin::default().add(a, b).await;
        serde_json::to_string(&result)
    })
}));

// 3. C# calls via FFI
[DllImport("HPD-Agent")]
extern IntPtr rust_execute_plugin_function(IntPtr name, IntPtr argsJson);
```

### Proposed Filter System (Same Pattern!)

```
Rust Filter (Macro) → Register Executor → C# FFI Call → Execute Rust Filter → Return JSON
```

**Code Flow:**
```rust
// 1. Define filter (SAME as plugin!)
#[hpd_filter("RequestCounter", "Counts requests")]
impl RequestCounter {
    #[filter_pre_invoke("Adds request count")]
    pub async fn pre_invoke(&mut self, context: FilterContext) -> FilterResult {
        // Modify context
    }
}

// 2. Register executor (SAME pattern as plugin!)
register_filter_executor("RequestCounter_pre", Arc::new(|context_json, state_json| {
    Box::pin(async move {
        let context: FilterContext = serde_json::from_str(&context_json)?;
        let mut filter: RequestCounter = serde_json::from_str(&state_json)?;
        let result = filter.pre_invoke(context).await;
        serde_json::to_string(&result)
    })
}));

// 3. C# calls via FFI (SAME as plugin!)
[DllImport("HPD-Agent")]
extern IntPtr rust_execute_filter(IntPtr name, IntPtr contextJson, IntPtr stateJson);
```

**It's literally the same architecture!**

---

## Technical Design

### 1. Rust Side Implementation

#### New Files Needed

```
hpd_rust_agent/src/filters.rs  (mirror of plugins.rs)
hpd_rust_agent_macros/src/filter_macro.rs  (mirror of plugin_macro.rs)
```

#### Core Types

```rust
// filters.rs
type FilterExecutor = Arc<dyn Fn(String, String) -> Pin<Box<dyn Future<Output = Result<String, String>> + Send>> + Send + Sync>;

static FILTER_EXECUTORS: Lazy<Mutex<HashMap<String, FilterExecutor>>> =
    Lazy::new(|| Mutex::new(HashMap::new()));

pub fn register_filter_executor(name: String, executor: FilterExecutor) {
    let mut registry = FILTER_EXECUTORS.lock().unwrap();
    registry.insert(name, executor);
}

pub async fn execute_filter_async(
    name: &str,
    context_json: String,
    state_json: String
) -> Result<String, String> {
    let executor = {
        let registry = FILTER_EXECUTORS.lock().unwrap();
        registry.get(name).cloned()
    };

    match executor {
        Some(exec) => exec(context_json, state_json).await,
        None => Err(format!("Filter '{}' not found", name))
    }
}

#[derive(Serialize, Deserialize)]
pub struct FilterContext {
    pub messages: Vec<Message>,
    pub options: Option<ChatOptions>,
    pub properties: HashMap<String, serde_json::Value>,
}

#[derive(Serialize, Deserialize)]
pub struct FilterResult {
    pub messages: Vec<Message>,
    pub new_state: String,  // JSON string
}

#[derive(Serialize, Deserialize)]
pub struct PostInvokeContext {
    pub request_messages: Vec<Message>,
    pub response_messages: Option<Vec<Message>>,
    pub exception: Option<String>,
    pub properties: HashMap<String, serde_json::Value>,
}

#[derive(Serialize, Deserialize)]
pub struct PostFilterResult {
    pub new_state: String,  // JSON string
}
```

#### FFI Exports

```rust
// ffi.rs
#[no_mangle]
pub extern "C" fn rust_execute_filter_pre_invoke(
    filter_name: *const c_char,
    context_json: *const c_char,
    state_json: *const c_char
) -> *mut c_char {
    // Validate pointers
    if filter_name.is_null() || context_json.is_null() || state_json.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let name = CStr::from_ptr(filter_name).to_str().unwrap();
        let context = CStr::from_ptr(context_json).to_str().unwrap().to_string();
        let state = CStr::from_ptr(state_json).to_str().unwrap().to_string();

        // Execute async filter
        let rt = tokio::runtime::Runtime::new().unwrap();
        let result = rt.block_on(async {
            execute_filter_async(name, context, state).await
        });

        match result {
            Ok(json_str) => match CString::new(json_str) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(e) => {
                eprintln!("Filter execution failed: {}", e);
                ptr::null_mut()
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn rust_execute_filter_post_invoke(
    filter_name: *const c_char,
    context_json: *const c_char,
    state_json: *const c_char
) -> *mut c_char {
    // Same implementation as above, but for post-invoke
}

#[no_mangle]
pub extern "C" fn rust_get_filter_registry() -> *mut c_char {
    let filters = get_registered_filters();
    let filter_data = serde_json::json!({
        "filters": filters
    });

    match serde_json::to_string(&filter_data) {
        Ok(json_str) => match CString::new(json_str) {
            Ok(c_string) => c_string.into_raw(),
            Err(_) => ptr::null_mut(),
        },
        Err(_) => ptr::null_mut(),
    }
}
```

#### Proc Macro

```rust
// hpd_rust_agent_macros/src/filter_macro.rs
#[proc_macro_attribute]
pub fn hpd_filter(args: TokenStream, input: TokenStream) -> TokenStream {
    // Similar to hpd_plugin macro
    // Generates:
    // - Filter registration code
    // - Executor registration for pre/post invoke
    // - JSON serialization/deserialization
    // - State management
}

#[proc_macro_attribute]
pub fn filter_pre_invoke(args: TokenStream, input: TokenStream) -> TokenStream {
    // Marks method as pre-invoke filter
}

#[proc_macro_attribute]
pub fn filter_post_invoke(args: TokenStream, input: TokenStream) -> TokenStream {
    // Marks method as post-invoke filter
}
```

### 2. C# Side Implementation

#### FFI Imports

```csharp
// NativeExports.cs
[UnmanagedCallersOnly(EntryPoint = "register_rust_filter")]
public static IntPtr RegisterRustFilter(
    IntPtr namePtr,
    IntPtr initialStatePtr)
{
    var name = Marshal.PtrToStringUTF8(namePtr);
    var initialState = Marshal.PtrToStringUTF8(initialStatePtr) ?? "{}";

    // Create C# wrapper filter
    var filter = new RustCallbackFilter(name, initialState);

    // Add to agent's filter list
    return StoreFilter(filter);
}

// Import Rust FFI functions
[DllImport("HPD-Agent", EntryPoint = "rust_execute_filter_pre_invoke")]
private static extern IntPtr RustExecuteFilterPreInvoke(
    IntPtr filterName,
    IntPtr contextJson,
    IntPtr stateJson);

[DllImport("HPD-Agent", EntryPoint = "rust_execute_filter_post_invoke")]
private static extern IntPtr RustExecuteFilterPostInvoke(
    IntPtr filterName,
    IntPtr contextJson,
    IntPtr stateJson);

[DllImport("HPD-Agent", EntryPoint = "rust_get_filter_registry")]
private static extern IntPtr RustGetFilterRegistry();
```

#### C# Wrapper Filter

```csharp
// Filters/PromptFiltering/RustCallbackFilter.cs
internal class RustCallbackFilter : IPromptFilter
{
    private readonly string _name;
    private string _state;

    public RustCallbackFilter(string name, string initialState)
    {
        _name = name;
        _state = initialState;
    }

    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptFilterContext context,
        Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next)
    {
        try
        {
            // Serialize context to JSON
            var contextJson = JsonSerializer.Serialize(new
            {
                messages = context.Messages,
                options = context.Options,
                properties = context.Properties
            });

            // Call Rust filter via FFI
            var namePtr = Marshal.StringToHGlobalAnsi(_name);
            var contextPtr = Marshal.StringToHGlobalAnsi(contextJson);
            var statePtr = Marshal.StringToHGlobalAnsi(_state);

            try
            {
                var resultPtr = NativeExports.RustExecuteFilterPreInvoke(
                    namePtr,
                    contextPtr,
                    statePtr);

                if (resultPtr == IntPtr.Zero)
                {
                    throw new Exception($"Rust filter '{_name}' returned null");
                }

                var resultJson = Marshal.PtrToStringUTF8(resultPtr);
                NativeExports.FreeString(resultPtr);

                // Deserialize result
                var result = JsonSerializer.Deserialize<FilterResult>(resultJson);

                // Update state
                _state = result.NewState;

                // Update messages
                context.Messages = result.Messages;
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
                Marshal.FreeHGlobal(contextPtr);
                Marshal.FreeHGlobal(statePtr);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't break the pipeline
            Console.WriteLine($"Rust filter '{_name}' failed: {ex.Message}");
        }

        return await next(context);
    }

    public async Task PostInvokeAsync(
        PostInvokeContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Serialize post-invoke context
            var contextJson = JsonSerializer.Serialize(new
            {
                requestMessages = context.RequestMessages,
                responseMessages = context.ResponseMessages,
                exception = context.Exception?.Message,
                properties = context.Properties
            });

            // Call Rust filter
            var namePtr = Marshal.StringToHGlobalAnsi(_name);
            var contextPtr = Marshal.StringToHGlobalAnsi(contextJson);
            var statePtr = Marshal.StringToHGlobalAnsi(_state);

            try
            {
                var resultPtr = NativeExports.RustExecuteFilterPostInvoke(
                    namePtr,
                    contextPtr,
                    statePtr);

                if (resultPtr != IntPtr.Zero)
                {
                    var resultJson = Marshal.PtrToStringUTF8(resultPtr);
                    NativeExports.FreeString(resultPtr);

                    var result = JsonSerializer.Deserialize<PostFilterResult>(resultJson);
                    _state = result.NewState;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
                Marshal.FreeHGlobal(contextPtr);
                Marshal.FreeHGlobal(statePtr);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw
            Console.WriteLine($"Rust post-invoke filter '{_name}' failed: {ex.Message}");
        }
    }

    private class FilterResult
    {
        public List<ChatMessage> Messages { get; set; }
        public string NewState { get; set; }
    }

    private class PostFilterResult
    {
        public string NewState { get; set; }
    }
}
```

#### AgentBuilder Integration

```csharp
// Agent/AgentBuilder.cs
public AgentBuilder WithRustFilter(string filterName, string initialState = "{}")
{
    var filter = new RustCallbackFilter(filterName, initialState);
    _promptFilters.Add(filter);
    return this;
}
```

---

## State Management

### State Flow

```
1. Filter Created:
   Rust: #[hpd_filter] macro generates default state
   C#: RustCallbackFilter initialized with state JSON

2. Pre-Invoke:
   C# → Serialize(context, state) → Rust
   Rust → Execute filter → Modify state → Return JSON
   C# → Deserialize → Update _state

3. Post-Invoke:
   C# → Serialize(context, state) → Rust
   Rust → Extract data → Update state → Return JSON
   C# → Deserialize → Update _state

4. Thread Serialization:
   Thread.Serialize() → Include filter states
   {"filterStates": {"RequestCounter": {"count": 42}}}

5. Thread Deserialization:
   Thread.Deserialize() → Restore filter states
   RustCallbackFilter._state = filterStates["RequestCounter"]
```

### Serialization Example

```rust
#[derive(Serialize, Deserialize)]
struct RequestCounter {
    count: u32,
    insights: Vec<String>,
}

impl RequestCounter {
    pub async fn pre_invoke(&mut self, mut context: FilterContext) -> FilterResult {
        self.count += 1;

        context.messages.insert(0, Message {
            role: "system",
            content: format!("Request #{}", self.count)
        });

        FilterResult {
            messages: context.messages,
            new_state: serde_json::to_string(&self).unwrap()  // ✅ Serialize self
        }
    }
}
```

---

## Usage Examples

### Example 1: Request Counter

```rust
// Rust filter
#[hpd_filter("RequestCounter", "Tracks request count")]
impl RequestCounter {
    count: u32,

    #[filter_pre_invoke("Adds request number to context")]
    pub async fn pre_invoke(&mut self, mut ctx: FilterContext) -> FilterResult {
        ctx.messages.insert(0, Message {
            role: "system",
            content: format!("This is request #{}", self.count)
        });

        self.count += 1;

        FilterResult {
            messages: ctx.messages,
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}

// Usage in C#
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.Anthropic, "claude-3-5-sonnet", apiKey)
    .WithRustFilter("RequestCounter", "{\"count\": 0}")  // ✅ Initial state
    .Build();
```

### Example 2: Response Analyzer

```rust
#[hpd_filter("ResponseAnalyzer", "Extracts insights from responses")]
impl ResponseAnalyzer {
    insights: Vec<String>,

    #[filter_post_invoke("Analyzes response for insights")]
    pub async fn post_invoke(&mut self, ctx: PostInvokeContext) -> PostFilterResult {
        if let Some(responses) = ctx.response_messages {
            for msg in responses {
                if let Some(insight) = extract_key_insight(&msg.content) {
                    self.insights.push(insight);
                }
            }
        }

        PostFilterResult {
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}
```

### Example 3: Multi-Language Context

```rust
#[hpd_filter("LanguageContext", "Adds language-specific context")]
impl LanguageContext {
    preferred_language: String,

    #[filter_pre_invoke("Adds language preference")]
    pub async fn pre_invoke(&mut self, mut ctx: FilterContext) -> FilterResult {
        // Check if user specified language in properties
        if let Some(lang) = ctx.properties.get("language") {
            self.preferred_language = lang.as_str().unwrap().to_string();
        }

        if !self.preferred_language.is_empty() {
            ctx.messages.insert(0, Message {
                role: "system",
                content: format!("User prefers responses in: {}", self.preferred_language)
            });
        }

        FilterResult {
            messages: ctx.messages,
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}
```

---

## Comparison: Plugin vs Filter

| Aspect | Plugins | Filters |
|--------|---------|---------|
| **Purpose** | Add tools/functions | Modify context/extract data |
| **Callback Signature** | `Fn(String) -> Result<String>` | `Fn(String, String) -> Result<String>` |
| **Input** | Function args (JSON) | Context + State (JSON) |
| **Output** | Function result (JSON) | Modified context + New state (JSON) |
| **State** | Stateless (or in args) | Stateful (passed explicitly) |
| **Execution** | LLM calls via tool | Pre/post LLM invocation |
| **Macro** | `#[hpd_plugin]` | `#[hpd_filter]` |
| **FFI Function** | `rust_execute_plugin_function` | `rust_execute_filter_pre_invoke` |

**Code Similarity: ~95%**

---

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)
- [ ] Create `filters.rs` (mirror `plugins.rs`)
- [ ] Add filter executor registry
- [ ] Add FFI exports (`rust_execute_filter_*`)
- [ ] Create `RustCallbackFilter` in C#
- [ ] Add `WithRustFilter()` to `AgentBuilder`

### Phase 2: Macro Support (Week 2)
- [ ] Create `filter_macro.rs`
- [ ] Implement `#[hpd_filter]` macro
- [ ] Implement `#[filter_pre_invoke]` macro
- [ ] Implement `#[filter_post_invoke]` macro
- [ ] Generate executor registration code

### Phase 3: State Management (Week 3)
- [ ] Add state serialization to filter results
- [ ] Update thread serialization to include filter states
- [ ] Update thread deserialization to restore filter states
- [ ] Add state validation/migration support

### Phase 4: Testing & Documentation (Week 4)
- [ ] Unit tests for Rust filters
- [ ] Integration tests for C# ↔ Rust
- [ ] Example filters (counter, analyzer, etc.)
- [ ] API documentation
- [ ] Migration guide for existing filters

---

## Benefits

1. **Language Agnostic**
   - ✅ Rust filters (via macros)
   - ✅ Python filters (via PyO3 bindings)
   - ✅ JavaScript filters (via Node.js FFI)

2. **Reuses Existing Infrastructure**
   - ✅ Same FFI pattern as plugins
   - ✅ Same executor registry model
   - ✅ Same JSON communication

3. **Stateful**
   - ✅ State flows through JSON
   - ✅ Serializable for thread persistence
   - ✅ Can maintain conversation memory

4. **Powerful**
   - ✅ Pre-invoke: Modify messages, add context
   - ✅ Post-invoke: Extract insights, learn from responses
   - ✅ Full access to conversation context

---

## Risks & Mitigations

### Risk 1: Performance Overhead
**Concern:** FFI + JSON serialization adds latency

**Mitigation:**
- Filters run async (won't block)
- JSON serialization is fast (~microseconds)
- Can batch multiple filters in single FFI call
- Benchmark: Plugin FFI is <1ms overhead

### Risk 2: State Management Complexity
**Concern:** State synchronization between C# and Rust

**Mitigation:**
- State is immutable (passed by value)
- New state returned explicitly
- C# owns the state, Rust just transforms
- Clear ownership model

### Risk 3: Error Handling
**Concern:** Rust panics could crash C#

**Mitigation:**
- All FFI calls wrapped in `catch_unwind`
- C# wrapper has try-catch
- Errors logged but don't break pipeline
- Same pattern as plugins (proven safe)

---

## Success Criteria

1. ✅ Rust users can create filters with `#[hpd_filter]` macro
2. ✅ Filters can modify messages in pre-invoke
3. ✅ Filters can extract data in post-invoke
4. ✅ State persists across invocations
5. ✅ State serializes with threads
6. ✅ Performance < 1ms overhead per filter
7. ✅ Error handling doesn't crash C# process

---

## Alternative Considered: AIContextProvider

Microsoft's `AIContextProvider` was evaluated but **rejected** because:

❌ **Factory-based, not callback-based**
- Requires C# factory function
- Can't pass Python/Rust functions as factories

❌ **Serialization doesn't help FFI**
- Only serializes **data**, not **behavior**
- Still needs C# code to deserialize and execute

❌ **Abstract class, not interface**
- Can't be implemented from JSON
- Requires C# inheritance

❌ **Not designed for cross-language scenarios**
- Built for C#-to-C# distributed systems
- Assumes all code is C#

**Verdict:** Our callback-based plugin pattern is superior for FFI.

---

## Conclusion

We can enable Rust/Python/JavaScript filters by **cloning our existing plugin architecture**.

**Key Insight:** Plugins and filters are structurally identical:
- Both need callbacks from external languages
- Both communicate via JSON
- Both need state management
- Both use the same FFI infrastructure

**Implementation is straightforward:**
1. Copy `plugins.rs` → `filters.rs`
2. Copy plugin macros → filter macros
3. Copy FFI functions with filter signature
4. Add `RustCallbackFilter` wrapper in C#

**Estimated effort:** 2-3 weeks for a Rust engineer familiar with the plugin system.

**Result:** Users can write powerful, stateful filters in any language!

---

## Next Steps

1. **Review this proposal** with the team
2. **Approve architecture** and API design
3. **Assign to Rust engineer** for implementation
4. **Create tracking issues** for each phase
5. **Set up benchmarks** to measure performance

---

## Appendix: Code Checklist

### Rust Files to Create
- [ ] `hpd_rust_agent/src/filters.rs`
- [ ] `hpd_rust_agent_macros/src/filter_macro.rs`
- [ ] Update `hpd_rust_agent/src/ffi.rs`

### C# Files to Create
- [ ] `HPD-Agent/Filters/PromptFiltering/RustCallbackFilter.cs`
- [ ] Update `HPD-Agent/FFI/NativeExports.cs`
- [ ] Update `HPD-Agent/Agent/AgentBuilder.cs`

### Tests to Create
- [ ] Rust unit tests for filter execution
- [ ] C# integration tests for FFI
- [ ] End-to-end filter tests
- [ ] State serialization tests
- [ ] Performance benchmarks

### Documentation to Create
- [ ] API reference for `#[hpd_filter]`
- [ ] Filter development guide
- [ ] Migration guide for C# filters
- [ ] Example filter gallery

---

**Author:** AI Assistant
**Date:** 2025-01-10
**Status:** Proposal - Awaiting Review
**Estimated Effort:** 2-3 weeks (1 Rust engineer)
