# API Reference

Complete API documentation for the HPD Rust Agent Library.

## Table of Contents

- [Core Types](#core-types)
- [Agent Management](#agent-management)
- [Conversation System](#conversation-system)
- [Plugin System](#plugin-system)
- [Procedural Macros](#procedural-macros)
- [Configuration](#configuration)
- [Streaming](#streaming)
- [Error Handling](#error-handling)
- [FFI Interface](#ffi-interface)

## Core Types

### `RustAgent`

Represents an AI agent instance.

```rust
pub struct RustAgent {
    // Internal implementation hidden
}
```

**Methods:**
- Agents are created through `RustAgentBuilder` and managed automatically
- Implement `Drop` for automatic cleanup

### `RustAgentBuilder`

Builder pattern for creating AI agents with fluent API.

```rust
pub struct RustAgentBuilder {
    // Internal fields
}

impl RustAgentBuilder {
    /// Creates a new agent builder with the specified name
    pub fn new(name: &str) -> Self

    /// Sets the system instructions for the agent
    pub fn with_instructions(self, instructions: &str) -> Self

    /// Sets the maximum number of function calls per conversation
    pub fn with_max_function_calls(self, max_calls: i32) -> Self

    /// Sets the maximum conversation history length
    pub fn with_max_conversation_history(self, max_history: i32) -> Self

    /// Configures the agent to use OpenRouter with the specified model and API key
    pub fn with_openrouter(self, model: &str, api_key: &str) -> Self

    /// Configures the agent to use Ollama with the specified model
    pub fn with_ollama(self, model: &str) -> Self

    /// Adds a specific plugin instance to the agent
    pub fn with_plugin<P: Plugin>(self, plugin: P) -> Self

    /// Adds all globally registered plugins to the agent
    pub fn with_registered_plugins(self) -> Self

    /// Builds the agent with the configured settings
    pub fn build(self) -> Result<RustAgent, String>

    /// Returns the JSON configuration for debugging purposes
    pub fn debug_json(&self) -> String
}
```

**Example:**
```rust
let agent = RustAgentBuilder::new("my-agent")
    .with_instructions("You are a helpful assistant")
    .with_max_function_calls(10)
    .with_openrouter("google/gemini-2.5-pro", "your-api-key")
    .with_registered_plugins()
    .build()?;
```

### `AgentConfig`

Configuration structure for agents (used internally).

```rust
pub struct AgentConfig {
    pub name: String,
    pub instructions: String,
    pub max_function_calls: i32,
    pub max_conversation_history: i32,
    pub llm_config: LlmConfig,
    pub plugins: Vec<String>,
}
```

## Agent Management

### Creating Agents

```rust
use hpd_rust_agent::{RustAgentBuilder, AppSettings};

// Load configuration
let config = AppSettings::load()?;
let api_key = config.get_openrouter_api_key()
    .ok_or("API key not found")?;

// Create agent
let agent = RustAgentBuilder::new("assistant")
    .with_instructions("You are a helpful AI assistant")
    .with_openrouter("google/gemini-2.5-pro", api_key)
    .build()?;
```

### Provider Configuration

#### OpenRouter
```rust
let agent = RustAgentBuilder::new("agent")
    .with_openrouter("google/gemini-2.5-pro", "your-api-key")
    .build()?;
```

#### Ollama
```rust
let agent = RustAgentBuilder::new("agent")
    .with_ollama("llama3.2:latest")
    .build()?;
```

## Conversation System

### `RustConversation`

Manages conversations with one or more AI agents.

```rust
pub struct RustConversation {
    // Internal implementation
}

impl RustConversation {
    /// Creates a new conversation with the specified agents
    pub fn new(agents: Vec<RustAgent>) -> Result<Self, String>

    /// Sends a message and returns the response synchronously
    pub fn send(&self, message: &str) -> Result<String, String>

    /// Sends a message and returns a stream of response events
    pub fn send_streaming(&self, message: &str) -> Result<impl Stream<Item = String>, String>
}

impl Drop for RustConversation {
    /// Automatically cleans up C# resources
    fn drop(&mut self)
}
```

**Example:**
```rust
// Create conversation
let conversation = RustConversation::new(vec![agent])?;

// Send synchronous message
let response = conversation.send("Hello, world!")?;
println!("Response: {}", response);

// Send streaming message
let mut stream = conversation.send_streaming("Tell me a story")?;
while let Some(event) = stream.next().await {
    println!("Event: {}", event);
}
```

## Plugin System

### `Plugin` Trait

Auto-implemented by the `#[hpd_plugin]` macro.

```rust
pub trait Plugin {
    /// Registers the plugin's functions in the global registry
    fn register_functions(&self);

    /// Returns metadata about the plugin's functions
    fn get_plugin_info(&self) -> Vec<RustFunctionInfo>;
}
```

### `RustFunctionInfo`

Metadata about AI-callable functions.

```rust
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RustFunctionInfo {
    pub name: String,
    pub description: String,
    pub parameters_schema: Value,
    pub requires_permission: bool,
}
```

### Plugin Registration

#### Global Registry Functions

```rust
/// Registers a plugin in the global registry
pub fn register_plugin(registration: PluginRegistration)

/// Returns all registered plugins
pub fn get_registered_plugins() -> Vec<String>

/// Returns statistics about registered plugins
pub fn get_plugin_stats() -> Vec<String>
```

#### `PluginRegistration`

Structure for manual plugin registration.

```rust
pub struct PluginRegistration {
    pub name: String,
    pub description: String,
    pub functions: Vec<(String, String)>,  // (name, wrapper_function)
    pub schemas: std::collections::HashMap<String, serde_json::Value>,
}
```

**Example:**
```rust
use hpd_rust_agent::{register_plugin, PluginRegistration};

let registration = PluginRegistration {
    name: "MyPlugin".to_string(),
    description: "Custom plugin".to_string(),
    functions: vec![
        ("my_function".to_string(), "my_function_wrapper".to_string()),
    ],
    schemas: Default::default(),
};

register_plugin(registration);
```

## Procedural Macros

### `#[hpd_plugin]`

Marks an implementation block as a plugin.

**Syntax:**
```rust
#[hpd_plugin("PluginName", "Plugin description")]
impl StructName {
    // AI functions go here
}
```

**Generated Code:**
- Plugin trait implementation
- Auto-registration constructor
- FFI wrapper functions
- JSON schema generation

**Example:**
```rust
#[derive(Default)]
pub struct MathPlugin;

#[hpd_plugin("Math", "Mathematical operations")]
impl MathPlugin {
    #[ai_function("Adds two numbers")]
    pub async fn add(&self, a: f64, b: f64) -> String {
        (a + b).to_string()
    }
}
```

### `#[ai_function]`

Marks a method as an AI-callable function.

**Syntax:**
```rust
#[ai_function("Function description")]
pub async fn function_name(&self, param: Type) -> String

// With custom name
#[ai_function("Description", name = "custom_name")]
pub async fn function_name(&self) -> String
```

**Requirements:**
- Must be inside an `#[hpd_plugin]` implementation
- Must be `async`
- Must return `String`
- First parameter must be `&self`

**Generated:**
- FFI wrapper function
- JSON schema for parameters
- Function registration code

**Example:**
```rust
#[ai_function("Calculates the square root of a number")]
pub async fn sqrt(&self, number: f64) -> String {
    if number < 0.0 {
        serde_json::json!({"error": "Cannot calculate square root of negative number"}).to_string()
    } else {
        serde_json::json!({"result": number.sqrt()}).to_string()
    }
}
```

### `#[requires_permission]`

Marks a function as requiring user permission.

**Syntax:**
```rust
#[ai_function("Dangerous operation")]
#[requires_permission]
pub async fn dangerous_function(&self) -> String
```

**Behavior:**
- Function will trigger permission request in C# UI
- User can approve or deny the operation
- Adds `requires_permission: true` to function metadata

**Example:**
```rust
#[ai_function("Permanently deletes a file")]
#[requires_permission]
pub async fn delete_file(&self, path: String) -> String {
    match std::fs::remove_file(&path) {
        Ok(()) => serde_json::json!({"success": true}).to_string(),
        Err(e) => serde_json::json!({"error": e.to_string()}).to_string(),
    }
}
```

## Configuration

### `AppSettings`

Manages application configuration from `appsettings.json`.

```rust
pub struct AppSettings {
    // Internal fields
}

impl AppSettings {
    /// Loads configuration from appsettings.json
    pub fn load() -> Result<Self, Box<dyn std::error::Error>>

    /// Returns the OpenRouter API key
    pub fn get_openrouter_api_key(&self) -> Option<&str>

    /// Returns the default AI model
    pub fn get_default_model(&self) -> Option<&str>

    /// Returns the default agent instructions
    pub fn get_default_instructions(&self) -> Option<&str>
}
```

**Configuration File Format:**
```json
{
  "OpenRouterApiKey": "your-api-key-here",
  "DefaultModel": "google/gemini-2.5-pro",
  "DefaultInstructions": "You are a helpful AI assistant."
}
```

**Example:**
```rust
let config = AppSettings::load()?;
let api_key = config.get_openrouter_api_key()
    .ok_or("OpenRouter API key not configured")?;
let model = config.get_default_model()
    .unwrap_or("google/gemini-2.5-pro");
```

## Streaming

### Stream Events

The streaming system produces JSON events representing different stages of AI processing:

#### Event Types

**STEP_STARTED**
```json
{
  "type": "STEP_STARTED",
  "step": "Processing user request"
}
```

**TEXT_MESSAGE_CONTENT**
```json
{
  "type": "TEXT_MESSAGE_CONTENT",
  "content": "Here is my response..."
}
```

**FUNCTION_CALL_STARTED**
```json
{
  "type": "FUNCTION_CALL_STARTED",
  "function_name": "calculate",
  "arguments": {"x": 5, "y": 10}
}
```

**FUNCTION_CALL_RESULT**
```json
{
  "type": "FUNCTION_CALL_RESULT",
  "function_name": "calculate",
  "result": "15"
}
```

### Streaming Example

```rust
use tokio_stream::StreamExt;

let mut stream = conversation.send_streaming("Calculate 2 + 2")?;

while let Some(event_json) = stream.next().await {
    let event: serde_json::Value = serde_json::from_str(&event_json)?;
    
    match event["type"].as_str() {
        Some("TEXT_MESSAGE_CONTENT") => {
            if let Some(content) = event["content"].as_str() {
                print!("{}", content);
            }
        }
        Some("FUNCTION_CALL_STARTED") => {
            println!("Calling function: {}", event["function_name"]);
        }
        Some("FUNCTION_CALL_RESULT") => {
            println!("Function result: {}", event["result"]);
        }
        _ => {
            println!("Other event: {}", event_json);
        }
    }
}
```

## Error Handling

### Error Types

Most functions return `Result<T, String>` where the error string contains details about what went wrong.

### Common Error Scenarios

**Configuration Errors:**
```rust
// Missing API key
let config = AppSettings::load()?;
let api_key = config.get_openrouter_api_key()
    .ok_or("OpenRouter API key not found in configuration")?;
```

**Agent Building Errors:**
```rust
let agent = RustAgentBuilder::new("agent")
    .with_openrouter("invalid-model", "invalid-key")
    .build()
    .map_err(|e| format!("Failed to create agent: {}", e))?;
```

**Conversation Errors:**
```rust
let response = conversation.send("Hello")
    .map_err(|e| format!("Failed to send message: {}", e))?;
```

### Error Handling Best Practices

```rust
use std::error::Error;

fn create_agent_safely() -> Result<RustAgent, Box<dyn Error>> {
    let config = AppSettings::load()
        .map_err(|e| format!("Configuration error: {}", e))?;
    
    let api_key = config.get_openrouter_api_key()
        .ok_or("Missing OpenRouter API key")?;
    
    let agent = RustAgentBuilder::new("safe-agent")
        .with_instructions("You are a helpful assistant")
        .with_openrouter("google/gemini-2.5-pro", api_key)
        .build()
        .map_err(|e| format!("Agent creation failed: {}", e))?;
    
    Ok(agent)
}
```

## FFI Interface

### Low-Level FFI Functions

These functions are used internally but can be called directly for advanced use cases:

```rust
extern "C" {
    /// Basic connectivity test
    pub fn ping(message: *const c_char) -> *mut c_char;
    
    /// Creates an agent from JSON configuration
    pub fn create_agent(config_json: *const c_char) -> *mut c_void;
    
    /// Creates a conversation with agents
    pub fn create_conversation(agents_json: *const c_char) -> *mut c_void;
    
    /// Sends a message synchronously
    pub fn send_message(
        conversation: *mut c_void,
        message: *const c_char
    ) -> *mut c_char;
    
    /// Sends a message with streaming
    pub fn send_streaming_message(
        conversation: *mut c_void,
        message: *const c_char,
        callback: extern "C" fn(*const c_char, *mut c_void),
        user_data: *mut c_void
    ) -> i32;
    
    /// Destroys an agent
    pub fn destroy_agent(agent: *mut c_void);
    
    /// Destroys a conversation
    pub fn destroy_conversation(conversation: *mut c_void);
    
    /// Frees C# allocated strings
    pub fn free_string(ptr: *mut c_void);
}
```

### Memory Management

**Important:** All strings returned from C# must be freed using `free_string()`:

```rust
unsafe {
    let response_ptr = send_message(conversation, message.as_ptr());
    let response_cstr = CStr::from_ptr(response_ptr);
    let response_string = response_cstr.to_str().unwrap().to_string();
    
    // CRITICAL: Free the C# allocated string
    free_string(response_ptr as *mut c_void);
}
```

## JSON Schema Generation

### Automatic Schema Generation

The `#[ai_function]` macro automatically generates OpenAI-compatible schemas:

**Rust Function:**
```rust
#[ai_function("Calculates the area of a rectangle")]
pub async fn calculate_area(&self, width: f64, height: f64, unit: String) -> String {
    // Implementation
}
```

**Generated Schema:**
```json
{
  "type": "function",
  "function": {
    "name": "calculate_area",
    "description": "Calculates the area of a rectangle",
    "parameters": {
      "type": "object",
      "properties": {
        "width": {
          "type": "number",
          "description": "Parameter width"
        },
        "height": {
          "type": "number",
          "description": "Parameter height"
        },
        "unit": {
          "type": "string",
          "description": "Parameter unit"
        }
      },
      "required": ["width", "height", "unit"]
    }
  }
}
```

### Supported Parameter Types

| Rust Type | JSON Schema Type | Notes |
|-----------|------------------|-------|
| `String` | `"string"` | |
| `i32`, `i64` | `"integer"` | |
| `f32`, `f64` | `"number"` | |
| `bool` | `"boolean"` | |
| `Option<T>` | Same as `T` | Not included in `required` array |
| `Vec<T>` | `"array"` with items of type `T` | |

## Performance Considerations

### Memory Usage
- Agents and conversations are automatically cleaned up
- Strings are efficiently managed across FFI boundary
- Plugin registration happens once at startup

### Async Performance
- Streaming uses efficient callback-based architecture
- Tokio runtime handles async operations efficiently
- No blocking operations in async functions

### Best Practices
```rust
// Good: Reuse conversations
let conversation = RustConversation::new(vec![agent])?;
for message in messages {
    let response = conversation.send(&message)?;
    // Process response
}

// Good: Use streaming for long responses
let mut stream = conversation.send_streaming("Long request")?;
while let Some(event) = stream.next().await {
    // Process events as they arrive
}

// Good: Register plugins once at startup
// (Automatic with #[hpd_plugin] macro)
```

---

This API reference covers all public interfaces in the HPD Rust Agent Library. For examples and tutorials, see the main [README](README.md).
