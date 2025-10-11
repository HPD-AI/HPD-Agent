# FFI Filter System - Complete Architecture Addendum

## Overview

After surveying all filter implementations in the codebase, we've identified **THREE distinct filter systems** that need FFI support:

1. **`IPromptFilter`** - Pre/post LLM invocation (context injection, memory extraction)
2. **`IAiFunctionFilter`** - Tool execution pipeline (permissions, logging, observability)
3. **`IMessageTurnFilter`** - Post-conversation processing (state capture, analytics)

**Good news:** All three use the **same callback pattern** and can share the same FFI infrastructure!

---

## Complete Filter Taxonomy

### 1. IPromptFilter (LLM Context Filters)

**Purpose:** Modify context before/after LLM calls

**Interface:**
```csharp
public interface IPromptFilter
{
    Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptFilterContext context,
        Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next);

    Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken);
}
```

**Existing Implementations:**
- ✅ `ProjectInjectedMemoryFilter` - Injects project documents
- ✅ `AgentInjectedMemoryFilter` - Injects agent memories
- ✅ `AgentPlanFilter` - Adds planning instructions
- ✅ `ExampleMemoryExtractionFilter` - Extracts memories from responses
- ✅ `ExampleDocumentUsageFilter` - Tracks document usage

**Use Cases:**
- Context injection (documents, memories, plans)
- Message transformation (redaction, summarization)
- Tool injection (dynamic tools based on context)
- Post-processing (memory extraction, learning)

---

### 2. IAiFunctionFilter (Tool Execution Filters)

**Purpose:** Intercept and control tool/function execution

**Interface:**
```csharp
public interface IAiFunctionFilter
{
    Task InvokeAsync(
        AiFunctionContext context,
        Func<AiFunctionContext, Task> next);
}
```

**Context:**
```csharp
public class AiFunctionContext
{
    public ToolCallRequest ToolCallRequest { get; }  // Function name + arguments
    public string? AgentName { get; set; }
    public AgentRunContext? RunContext { get; set; }  // Run statistics, iteration count
    public bool IsTerminated { get; set; }
    public object? Result { get; set; }
    public AIFunction? Function { get; set; }
    public Dictionary<string, object> Metadata { get; }
}
```

**Existing Implementations:**
- ✅ `AGUIPermissionFilter` - Web-based permission system
- ✅ `ConsolePermissionFilter` - CLI permission system
- ✅ `AutoApprovePermissionFilter` - Auto-approve for testing
- ✅ `LoggingAiFunctionFilter` - Logs function inputs/outputs
- ✅ `ObservabilityAiFunctionFilter` - OpenTelemetry spans + metrics

**Use Cases:**
- Permission/authorization checks
- Logging and auditing
- Observability (metrics, traces)
- Rate limiting
- Circuit breakers
- Retry logic
- Function call transformation

---

### 3. IMessageTurnFilter (Conversation Turn Filters)

**Purpose:** Process completed message turns (after all tools complete)

**Interface:**
```csharp
public interface IMessageTurnFilter
{
    Task InvokeAsync(
        MessageTurnFilterContext context,
        Func<MessageTurnFilterContext, Task> next);
}
```

**Use Cases:**
- State capture (save conversation state changes)
- Analytics (conversation metrics)
- Notifications (alert on specific patterns)
- Backup/archival
- Post-conversation processing

---

## Unified FFI Architecture

**Key Insight:** All three filter types use the **same middleware pattern**:
- `context` → Callback → `next(context)` → Result

This means we can use **one unified FFI system** for all filters!

### Rust Side: Unified Filter System

```rust
// filters.rs - Single file for all filter types

// ========================================
// 1. PROMPT FILTERS (IPromptFilter)
// ========================================

type PromptFilterExecutor = Arc<dyn Fn(String, String) -> Pin<Box<dyn Future<Output = Result<String, String>> + Send>> + Send + Sync>;

static PROMPT_FILTER_EXECUTORS: Lazy<Mutex<HashMap<String, PromptFilterExecutor>>> =
    Lazy::new(|| Mutex::new(HashMap::new()));

pub fn register_prompt_filter_executor(name: String, executor: PromptFilterExecutor) {
    PROMPT_FILTER_EXECUTORS.lock().unwrap().insert(name, executor);
}

pub async fn execute_prompt_filter_async(
    name: &str,
    context_json: String,
    state_json: String
) -> Result<String, String> {
    let executor = PROMPT_FILTER_EXECUTORS.lock().unwrap().get(name).cloned();
    match executor {
        Some(exec) => exec(context_json, state_json).await,
        None => Err(format!("Prompt filter '{}' not found", name))
    }
}

#[derive(Serialize, Deserialize)]
pub struct PromptFilterContext {
    pub messages: Vec<Message>,
    pub options: Option<ChatOptions>,
    pub properties: HashMap<String, serde_json::Value>,
}

#[derive(Serialize, Deserialize)]
pub struct PromptFilterResult {
    pub messages: Vec<Message>,
    pub new_state: String,
}

// ========================================
// 2. FUNCTION FILTERS (IAiFunctionFilter)
// ========================================

type FunctionFilterExecutor = Arc<dyn Fn(String, String) -> Pin<Box<dyn Future<Output = Result<String, String>> + Send>> + Send + Sync>;

static FUNCTION_FILTER_EXECUTORS: Lazy<Mutex<HashMap<String, FunctionFilterExecutor>>> =
    Lazy::new(|| Mutex::new(HashMap::new()));

pub fn register_function_filter_executor(name: String, executor: FunctionFilterExecutor) {
    FUNCTION_FILTER_EXECUTORS.lock().unwrap().insert(name, executor);
}

pub async fn execute_function_filter_async(
    name: &str,
    context_json: String,
    state_json: String
) -> Result<String, String> {
    let executor = FUNCTION_FILTER_EXECUTORS.lock().unwrap().get(name).cloned();
    match executor {
        Some(exec) => exec(context_json, state_json).await,
        None => Err(format!("Function filter '{}' not found", name))
    }
}

#[derive(Serialize, Deserialize)]
pub struct AiFunctionContext {
    pub function_name: String,
    pub arguments: HashMap<String, serde_json::Value>,
    pub agent_name: Option<String>,
    pub run_context: Option<AgentRunContext>,
    pub metadata: HashMap<String, serde_json::Value>,
}

#[derive(Serialize, Deserialize)]
pub struct AiFunctionFilterResult {
    pub is_terminated: bool,
    pub result: Option<String>,
    pub new_state: String,
}

#[derive(Serialize, Deserialize)]
pub struct AgentRunContext {
    pub run_id: String,
    pub conversation_id: String,
    pub current_iteration: u32,
    pub max_iterations: u32,
    pub completed_functions: Vec<String>,
}

// ========================================
// 3. MESSAGE TURN FILTERS (IMessageTurnFilter)
// ========================================

type MessageTurnFilterExecutor = Arc<dyn Fn(String, String) -> Pin<Box<dyn Future<Output = Result<String, String>> + Send>> + Send + Sync>;

static MESSAGE_TURN_FILTER_EXECUTORS: Lazy<Mutex<HashMap<String, MessageTurnFilterExecutor>>> =
    Lazy::new(|| Mutex::new(HashMap::new()));

pub fn register_message_turn_filter_executor(name: String, executor: MessageTurnFilterExecutor) {
    MESSAGE_TURN_FILTER_EXECUTORS.lock().unwrap().insert(name, executor);
}

pub async fn execute_message_turn_filter_async(
    name: &str,
    context_json: String,
    state_json: String
) -> Result<String, String> {
    let executor = MESSAGE_TURN_FILTER_EXECUTORS.lock().unwrap().get(name).cloned();
    match executor {
        Some(exec) => exec(context_json, state_json).await,
        None => Err(format!("Message turn filter '{}' not found", name))
    }
}

#[derive(Serialize, Deserialize)]
pub struct MessageTurnContext {
    pub user_message: Message,
    pub turn_history: Vec<Message>,
    pub options: Option<ChatOptions>,
}

#[derive(Serialize, Deserialize)]
pub struct MessageTurnFilterResult {
    pub new_state: String,
}
```

### Rust Macros: Unified Filter Declaration

```rust
// Filters don't need descriptions - they're internal implementation details, not user-facing like plugins!

#[hpd_filter(PromptFilter)]
impl MyPromptFilter {
    count: u32,

    #[filter_pre_invoke]
    pub async fn pre_invoke(&mut self, ctx: PromptFilterContext) -> PromptFilterResult {
        // Modify messages
        ctx.messages.insert(0, Message {
            role: "system",
            content: format!("Request #{}", self.count)
        });
        self.count += 1;

        PromptFilterResult {
            messages: ctx.messages,
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}

#[hpd_filter(FunctionFilter)]
impl MyPermissionFilter {
    approved_functions: Vec<String>,

    #[filter_invoke]
    pub async fn invoke(&mut self, ctx: AiFunctionContext) -> AiFunctionFilterResult {
        let approved = self.approved_functions.contains(&ctx.function_name);

        AiFunctionFilterResult {
            is_terminated: !approved,
            result: if approved { None } else { Some("Permission denied".to_string()) },
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}

#[hpd_filter(MessageTurnFilter)]
impl MyAnalyticsFilter {
    total_turns: u32,

    #[filter_invoke]
    pub async fn invoke(&mut self, ctx: MessageTurnContext) -> MessageTurnFilterResult {
        self.total_turns += 1;
        // Track metrics

        MessageTurnFilterResult {
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}
```

### C# Side: Unified Wrapper System

```csharp
// Unified base class for all callback filters
internal abstract class RustCallbackFilterBase
{
    protected readonly string _name;
    protected string _state;

    protected RustCallbackFilterBase(string name, string initialState)
    {
        _name = name;
        _state = initialState;
    }

    protected async Task<string> InvokeRustFilter(
        string filterType,
        string contextJson)
    {
        var namePtr = Marshal.StringToHGlobalAnsi(_name);
        var contextPtr = Marshal.StringToHGlobalAnsi(contextJson);
        var statePtr = Marshal.StringToHGlobalAnsi(_state);

        try
        {
            IntPtr resultPtr = filterType switch
            {
                "prompt_pre" => NativeExports.RustExecutePromptFilterPreInvoke(namePtr, contextPtr, statePtr),
                "prompt_post" => NativeExports.RustExecutePromptFilterPostInvoke(namePtr, contextPtr, statePtr),
                "function" => NativeExports.RustExecuteFunctionFilter(namePtr, contextPtr, statePtr),
                "message_turn" => NativeExports.RustExecuteMessageTurnFilter(namePtr, contextPtr, statePtr),
                _ => throw new ArgumentException($"Unknown filter type: {filterType}")
            };

            if (resultPtr == IntPtr.Zero)
            {
                throw new Exception($"Rust filter '{_name}' returned null");
            }

            var resultJson = Marshal.PtrToStringUTF8(resultPtr);
            NativeExports.FreeString(resultPtr);
            return resultJson;
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
            Marshal.FreeHGlobal(contextPtr);
            Marshal.FreeHGlobal(statePtr);
        }
    }
}

// IPromptFilter wrapper
internal class RustPromptFilter : RustCallbackFilterBase, IPromptFilter
{
    public RustPromptFilter(string name, string initialState)
        : base(name, initialState) { }

    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptFilterContext context,
        Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next)
    {
        var contextJson = JsonSerializer.Serialize(new {
            messages = context.Messages,
            options = context.Options,
            properties = context.Properties
        });

        var resultJson = await InvokeRustFilter("prompt_pre", contextJson);
        var result = JsonSerializer.Deserialize<PromptFilterResult>(resultJson);

        _state = result.NewState;
        context.Messages = result.Messages;

        return await next(context);
    }

    public async Task PostInvokeAsync(PostInvokeContext context, CancellationToken ct)
    {
        var contextJson = JsonSerializer.Serialize(new {
            requestMessages = context.RequestMessages,
            responseMessages = context.ResponseMessages,
            exception = context.Exception?.Message,
            properties = context.Properties
        });

        var resultJson = await InvokeRustFilter("prompt_post", contextJson);
        var result = JsonSerializer.Deserialize<PostFilterResult>(resultJson);
        _state = result.NewState;
    }
}

// IAiFunctionFilter wrapper
internal class RustFunctionFilter : RustCallbackFilterBase, IAiFunctionFilter
{
    public RustFunctionFilter(string name, string initialState)
        : base(name, initialState) { }

    public async Task InvokeAsync(
        AiFunctionContext context,
        Func<AiFunctionContext, Task> next)
    {
        var contextJson = JsonSerializer.Serialize(new {
            functionName = context.ToolCallRequest.FunctionName,
            arguments = context.ToolCallRequest.Arguments,
            agentName = context.AgentName,
            runContext = context.RunContext != null ? new {
                runId = context.RunContext.RunId,
                conversationId = context.RunContext.ConversationId,
                currentIteration = context.RunContext.CurrentIteration,
                maxIterations = context.RunContext.MaxIterations,
                completedFunctions = context.RunContext.CompletedFunctions
            } : null,
            metadata = context.Metadata
        });

        var resultJson = await InvokeRustFilter("function", contextJson);
        var result = JsonSerializer.Deserialize<FunctionFilterResult>(resultJson);

        _state = result.NewState;

        if (result.IsTerminated)
        {
            context.IsTerminated = true;
            if (result.Result != null)
            {
                context.Result = result.Result;
            }
            return;
        }

        await next(context);
    }
}

// IMessageTurnFilter wrapper
internal class RustMessageTurnFilter : RustCallbackFilterBase, IMessageTurnFilter
{
    public RustMessageTurnFilter(string name, string initialState)
        : base(name, initialState) { }

    public async Task InvokeAsync(
        MessageTurnFilterContext context,
        Func<MessageTurnFilterContext, Task> next)
    {
        var contextJson = JsonSerializer.Serialize(new {
            userMessage = context.UserMessage,
            turnHistory = context.TurnHistory,
            options = context.Options
        });

        var resultJson = await InvokeRustFilter("message_turn", contextJson);
        var result = JsonSerializer.Deserialize<MessageTurnFilterResult>(resultJson);
        _state = result.NewState;

        await next(context);
    }
}
```

---

## Special Considerations

### 1. Scoped Filters (IAiFunctionFilter only)

The `ScopedFilterSystem` allows filters to target specific plugins or functions:

```csharp
// Scoped filter manager
public class ScopedFilterManager
{
    public void AddFilter(IAiFunctionFilter filter, FilterScope scope, string? target = null);
    public IEnumerable<IAiFunctionFilter> GetApplicableFilters(string functionName, string? pluginTypeName);
}

public enum FilterScope
{
    Global,    // Applies to all functions
    Plugin,    // Applies to functions from specific plugin
    Function   // Applies to specific function only
}
```

**FFI Support:**
```rust
#[hpd_filter(FunctionFilter, scope = Plugin, target = "MathPlugin")]
impl MyPluginFilter {
    // Only applies to MathPlugin functions
}

#[hpd_filter(FunctionFilter, scope = Function, target = "dangerous_function")]
impl MyFunctionFilter {
    // Only applies to dangerous_function
}
```

### 2. Permission Filters

Permission filters are special `IAiFunctionFilter` implementations that:
- Check if function requires permission (`HPDOptions.RequiresPermission`)
- Store user decisions (allow/deny, with scope)
- Handle continuation permissions (when iteration limit reached)

**Rust Implementation:**
```rust
#[hpd_filter(FunctionFilter)]
impl RustPermissionFilter {
    approved_list: Vec<String>,
    denied_list: Vec<String>,

    #[filter_invoke]
    pub async fn invoke(&mut self, ctx: AiFunctionContext) -> AiFunctionFilterResult {
        // Check if function requires permission
        // (This flag would be passed in metadata or function info)

        if self.approved_list.contains(&ctx.function_name) {
            return AiFunctionFilterResult {
                is_terminated: false,
                result: None,
                new_state: serde_json::to_string(&self).unwrap()
            };
        }

        if self.denied_list.contains(&ctx.function_name) {
            return AiFunctionFilterResult {
                is_terminated: true,
                result: Some("Permission denied".to_string()),
                new_state: serde_json::to_string(&self).unwrap()
            };
        }

        // Request permission via callback to external system
        let approved = self.request_permission_external(&ctx.function_name, &ctx.arguments).await;

        if approved {
            self.approved_list.push(ctx.function_name.clone());
        } else {
            self.denied_list.push(ctx.function_name.clone());
        }

        AiFunctionFilterResult {
            is_terminated: !approved,
            result: if approved { None } else { Some("Permission denied".to_string()) },
            new_state: serde_json::to_string(&self).unwrap()
        }
    }

    async fn request_permission_external(&self, function_name: &str, args: &HashMap<String, Value>) -> bool {
        // Call external permission system (e.g., web UI, another service)
        // This would be another callback/FFI to host application
        true // Placeholder
    }
}
```

### 3. Observability Filters

`ObservabilityAiFunctionFilter` uses OpenTelemetry:

**Rust Equivalent:**
```rust
use opentelemetry::{trace::Tracer, metrics::Meter};

#[hpd_filter(FunctionFilter)]
impl RustObservabilityFilter {
    tracer: Option<Box<dyn Tracer>>,  // Would need careful FFI handling
    tool_call_counter: Counter<u64>,
    tool_call_duration: Histogram<f64>,

    #[filter_invoke]
    pub async fn invoke(&mut self, ctx: AiFunctionContext) -> AiFunctionFilterResult {
        let start = Instant::now();

        // Start span
        let span = self.tracer.as_ref()
            .map(|t| t.start(format!("execute_tool {}", ctx.function_name)));

        // Record metrics
        self.tool_call_counter.add(1, &[KeyValue::new("function", ctx.function_name.clone())]);

        // Execute filter (would actually need to modify to call next)
        // This is conceptual - real implementation would need callback chaining

        let duration = start.elapsed();
        self.tool_call_duration.record(duration.as_secs_f64() * 1000.0, &[]);

        AiFunctionFilterResult {
            is_terminated: false,
            result: None,
            new_state: serde_json::to_string(&self).unwrap()
        }
    }
}
```

---

## Complete FFI API Summary

### Rust → C# Exports

```rust
// Prompt filters
#[no_mangle]
pub extern "C" fn rust_execute_prompt_filter_pre_invoke(name: *const c_char, context_json: *const c_char, state_json: *const c_char) -> *mut c_char;

#[no_mangle]
pub extern "C" fn rust_execute_prompt_filter_post_invoke(name: *const c_char, context_json: *const c_char, state_json: *const c_char) -> *mut c_char;

// Function filters
#[no_mangle]
pub extern "C" fn rust_execute_function_filter(name: *const c_char, context_json: *const c_char, state_json: *const c_char) -> *mut c_char;

// Message turn filters
#[no_mangle]
pub extern "C" fn rust_execute_message_turn_filter(name: *const c_char, context_json: *const c_char, state_json: *const c_char) -> *mut c_char;

// Registry queries
#[no_mangle]
pub extern "C" fn rust_get_prompt_filter_registry() -> *mut c_char;

#[no_mangle]
pub extern "C" fn rust_get_function_filter_registry() -> *mut c_char;

#[no_mangle]
pub extern "C" fn rust_get_message_turn_filter_registry() -> *mut c_char;
```

### C# → Rust Imports

```csharp
// FFI imports in NativeExports.cs
[DllImport("HPD-Agent", EntryPoint = "rust_execute_prompt_filter_pre_invoke")]
private static extern IntPtr RustExecutePromptFilterPreInvoke(IntPtr name, IntPtr contextJson, IntPtr stateJson);

[DllImport("HPD-Agent", EntryPoint = "rust_execute_function_filter")]
private static extern IntPtr RustExecuteFunctionFilter(IntPtr name, IntPtr contextJson, IntPtr stateJson);

[DllImport("HPD-Agent", EntryPoint = "rust_execute_message_turn_filter")]
private static extern IntPtr RustExecuteMessageTurnFilter(IntPtr name, IntPtr contextJson, IntPtr stateJson);
```

---

## Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Create unified `filters.rs` with all three filter types
- [ ] Add filter executor registries (prompt, function, message turn)
- [ ] Add FFI exports for all filter types
- [ ] Create C# wrapper classes (`RustPromptFilter`, `RustFunctionFilter`, `RustMessageTurnFilter`)

### Phase 2: Macro Support
- [ ] Create unified `#[hpd_filter(FilterType, ...)]` macro
- [ ] Support all three filter types in macro
- [ ] Generate appropriate executor registration for each type
- [ ] Support scoped filters (Plugin, Function, Global)

### Phase 3: Special Features
- [ ] Permission filter support (callback to host for UI)
- [ ] Observability integration (OpenTelemetry FFI)
- [ ] Scoped filter targeting
- [ ] State serialization/deserialization

### Phase 4: Integration
- [ ] Update `AgentBuilder` with all filter types
- [ ] Thread serialization with filter states
- [ ] Thread deserialization with filter restoration

---

## Estimated Effort

| Component | Original Estimate | Updated Estimate |
|-----------|------------------|------------------|
| **IPromptFilter FFI** | 1 week | 1 week |
| **IAiFunctionFilter FFI** | +1 week | +1 week |
| **IMessageTurnFilter FFI** | +0.5 weeks | +0.5 weeks |
| **Unified system refactor** | N/A | +0.5 weeks (overlap reduction) |
| **Testing & docs** | 1 week | 1 week |
| **Total** | 3.5 weeks | **4 weeks** |

The good news: 80% of the code is reusable across all three filter types!

---

## Recommendation

**Implement all three filter types in one unified system:**

1. ✅ Reuse 80% of code across filter types
2. ✅ Consistent API for users
3. ✅ Single macro system
4. ✅ Unified FFI layer
5. ✅ Complete feature parity with C# filters

**Priority Order:**
1. **IAiFunctionFilter** (highest value - permissions, logging, observability)
2. **IPromptFilter** (medium value - context injection, memory extraction)
3. **IMessageTurnFilter** (lower value - post-conversation processing)

But implementing all three together is only marginally more work than implementing one!

---

**Next Step:** Review with team and assign to Rust engineer for unified implementation.
