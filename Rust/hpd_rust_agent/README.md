# HPD Rust Agent Library

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/Ewoofcoding/HPD-Agent)
[![Rust Version](https://img.shields.io/badge/rust-1.70%2B-blue.svg)](https://www.rust-lang.org)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A powerful, ergonomic Rust library for building AI agents with plugin support, real-time streaming, and seamless C# interoperability.

## 🚀 Features

- **🔧 Ergonomic Plugin System**: Zero-boilerplate plugin development with procedural macros
- **🌉 C# Interoperability**: Seamless FFI integration with HPD C# Agent framework
- **📡 Real-time Streaming**: Async streaming conversations with callback-based architecture
- **🔒 Memory Safety**: Full Rust memory safety with proper lifecycle management
- **⚡ High Performance**: Optimized for .NET AOT compilation and cross-platform deployment
- **🛡️ Security**: Built-in permission system for sensitive AI functions
- **📊 Auto-Schema Generation**: Automatic OpenAI function calling schema generation

## 📦 Installation

Add this to your `Cargo.toml`:

```toml
[dependencies]
hpd_rust_agent = "0.1.0"
hpd_rust_agent_macros = "0.1.0"
tokio = { version = "1.0", features = ["full"] }
serde = { version = "1.0", features = ["derive"] }
serde_json = "1.0"
```

## 🔧 Configuration

Create an `appsettings.json` file in your project root:

```json
{
  "OpenRouterApiKey": "your-api-key-here",
  "DefaultModel": "google/gemini-2.5-pro",
  "DefaultInstructions": "You are a helpful AI assistant."
}
```

## 🏗️ Architecture Overview

The HPD Rust Agent Library is built on 5 core modules:

### Module 1: FFI Bridge Foundation
- Cross-platform native library compilation
- Memory-safe string handling between C# and Rust
- Basic agent operations and lifecycle management

### Module 2: Object Lifecycle Management
- Agent configuration with external API integration
- Conversation management with proper memory cleanup
- JSON serialization optimized for .NET AOT

### Module 3: Conversation & Streaming
- Real-time streaming with callback-based architecture
- Microsoft.Extensions.AI integration
- Async message processing with proper event handling

### Module 4: Automated Plugin System
- Global plugin registry with runtime discovery
- JSON schema generation for AI function calling
- FFI integration for C# plugin consumption

### Module 5: Ergonomic Plugin Development
- Procedural macros for zero-boilerplate plugin creation
- Automatic plugin registration and trait implementation
- Full feature parity with C# attribute system

## 🚀 Quick Start

### Basic Agent Creation

```rust
use hpd_rust_agent::{RustAgentBuilder, AppSettings, RustConversation};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Load configuration
    let config = AppSettings::load()?;
    let api_key = config.get_openrouter_api_key()
        .ok_or("OpenRouter API key not found")?;
    
    // Create an agent
    let agent = RustAgentBuilder::new("my-agent")
        .with_instructions("You are a helpful assistant")
        .with_openrouter("google/gemini-2.5-pro", api_key)
        .build()?;
    
    // Create a conversation
    let conversation = RustConversation::new(vec![agent])?;
    
    // Send a message
    let response = conversation.send("Hello, world!")?;
    println!("Agent response: {}", response);
    
    Ok(())
}
```

### Creating Plugins

The HPD Rust Agent Library provides an extremely ergonomic way to create AI function plugins:

```rust
use hpd_rust_agent::{hpd_plugin, ai_function, requires_permission};

#[derive(Default)]
pub struct FileOperationsPlugin {
    operations_count: std::sync::atomic::AtomicU32,
}

#[hpd_plugin("FileOperations", "A plugin for safe file operations")]
impl FileOperationsPlugin {
    /// Safely reads file metadata
    #[ai_function("Gets information about a file")]
    pub async fn get_file_info(&self, path: String) -> String {
        // Increment counter
        self.operations_count.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        
        // Safe operation - just metadata
        match std::fs::metadata(&path) {
            Ok(metadata) => {
                serde_json::json!({
                    "path": path,
                    "size": metadata.len(),
                    "is_file": metadata.is_file(),
                    "is_dir": metadata.is_dir(),
                    "success": true
                }).to_string()
            }
            Err(e) => {
                serde_json::json!({
                    "error": e.to_string(),
                    "success": false
                }).to_string()
            }
        }
    }
    
    /// Dangerous operation requiring permission
    #[ai_function("Permanently deletes a file")]
    #[requires_permission]
    pub async fn delete_file(&self, path: String) -> String {
        // This function requires user permission
        match std::fs::remove_file(&path) {
            Ok(()) => {
                serde_json::json!({
                    "message": format!("Successfully deleted {}", path),
                    "success": true
                }).to_string()
            }
            Err(e) => {
                serde_json::json!({
                    "error": e.to_string(),
                    "success": false
                }).to_string()
            }
        }
    }
    
    /// Get plugin statistics
    #[ai_function("Returns usage statistics for this plugin")]
    pub async fn get_stats(&self) -> String {
        let count = self.operations_count.load(std::sync::atomic::Ordering::SeqCst);
        serde_json::json!({
            "operations_performed": count,
            "plugin_name": "FileOperations"
        }).to_string()
    }
}
```

### Using Plugins with Agents

```rust
use hpd_rust_agent::{RustAgentBuilder, AppSettings};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let config = AppSettings::load()?;
    let api_key = config.get_openrouter_api_key()
        .ok_or("OpenRouter API key not found")?;
    
    // Create plugin instance
    let file_plugin = FileOperationsPlugin::default();
    
    // Create agent with plugin
    let agent = RustAgentBuilder::new("file-agent")
        .with_instructions("You can help with file operations")
        .with_openrouter("google/gemini-2.5-pro", api_key)
        .with_plugin(file_plugin)                    // Add specific plugin
        .with_registered_plugins()                   // Add all auto-registered plugins
        .build()?;
    
    let conversation = RustConversation::new(vec![agent])?;
    
    // The AI can now call your plugin functions
    let response = conversation.send("What files are in the current directory?")?;
    println!("Response: {}", response);
    
    Ok(())
}
```

### Streaming Conversations

```rust
use hpd_rust_agent::{RustAgentBuilder, RustConversation};
use tokio_stream::StreamExt;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let config = AppSettings::load()?;
    let api_key = config.get_openrouter_api_key()
        .ok_or("OpenRouter API key not found")?;
    
    let agent = RustAgentBuilder::new("streaming-agent")
        .with_instructions("You provide detailed, step-by-step responses")
        .with_openrouter("google/gemini-2.5-pro", api_key)
        .build()?;
    
    let conversation = RustConversation::new(vec![agent])?;
    
    // Create streaming conversation
    let mut stream = conversation.send_streaming("Explain how to bake a cake")?;
    
    // Process events as they arrive
    while let Some(event_json) = stream.next().await {
        println!("Event: {}", event_json);
        // Parse and handle different event types:
        // - STEP_STARTED
        // - TEXT_MESSAGE_CONTENT 
        // - FUNCTION_CALL_STARTED
        // - etc.
    }
    
    Ok(())
}
```

## 📚 API Reference

### Core Types

#### `RustAgentBuilder`

Builder pattern for creating AI agents.

```rust
impl RustAgentBuilder {
    pub fn new(name: &str) -> Self
    pub fn with_instructions(self, instructions: &str) -> Self
    pub fn with_max_function_calls(self, max_calls: i32) -> Self
    pub fn with_max_conversation_history(self, max_history: i32) -> Self
    pub fn with_openrouter(self, model: &str, api_key: &str) -> Self
    pub fn with_ollama(self, model: &str) -> Self
    pub fn with_plugin<P: Plugin>(self, plugin: P) -> Self
    pub fn with_registered_plugins(self) -> Self
    pub fn build(self) -> Result<RustAgent, String>
}
```

#### `RustConversation`

Manages conversations with AI agents.

```rust
impl RustConversation {
    pub fn new(agents: Vec<RustAgent>) -> Result<Self, String>
    pub fn send(&self, message: &str) -> Result<String, String>
    pub fn send_streaming(&self, message: &str) -> Result<impl Stream<Item = String>, String>
}
```

#### `Plugin` Trait

Auto-implemented by the `#[hpd_plugin]` macro.

```rust
pub trait Plugin {
    fn register_functions(&self);
    fn get_plugin_info(&self) -> Vec<RustFunctionInfo>;
}
```

### Procedural Macros

#### `#[hpd_plugin]`

Marks an implementation block as a plugin. Automatically generates:
- Plugin registration code
- FFI wrapper functions
- JSON schema generation
- Plugin trait implementation

```rust
#[hpd_plugin("PluginName", "Plugin description")]
impl MyStruct {
    // AI functions go here
}
```

#### `#[ai_function]`

Marks a method as an AI-callable function.

```rust
#[ai_function("Function description")]
pub async fn my_function(&self, param: String) -> String {
    // Function implementation
}

// With custom name
#[ai_function("Description", name = "custom_name")]
pub async fn my_function(&self) -> String { /* ... */ }
```

#### `#[requires_permission]`

Marks a function as requiring user permission before execution.

```rust
#[ai_function("Dangerous operation")]
#[requires_permission]
pub async fn dangerous_function(&self) -> String {
    // Implementation
}
```

### Configuration

#### `AppSettings`

Manages application configuration from `appsettings.json`.

```rust
impl AppSettings {
    pub fn load() -> Result<Self, Box<dyn std::error::Error>>
    pub fn get_openrouter_api_key(&self) -> Option<&str>
    pub fn get_default_model(&self) -> Option<&str>
    pub fn get_default_instructions(&self) -> Option<&str>
}
```

## 🔧 Advanced Usage

### Custom Plugin Registration

```rust
use hpd_rust_agent::{register_plugin, PluginRegistration};

// Manual plugin registration (if not using macros)
let plugin_info = PluginRegistration {
    name: "CustomPlugin".to_string(),
    description: "My custom plugin".to_string(),
    functions: vec![
        ("function_name", "wrapper_function_name"),
    ],
    schemas: Default::default(),
};

register_plugin(plugin_info);
```

### Function Schema Generation

The library automatically generates OpenAI-compatible function schemas:

```json
{
  "type": "function",
  "function": {
    "name": "get_file_info",
    "description": "Gets information about a file",
    "parameters": {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "Parameter path"
        }
      },
      "required": ["path"]
    }
  }
}
```

### Error Handling

```rust
use hpd_rust_agent::{RustAgentBuilder, AppSettings};

fn create_agent() -> Result<RustAgent, Box<dyn std::error::Error>> {
    let config = AppSettings::load()
        .map_err(|e| format!("Failed to load config: {}", e))?;
    
    let api_key = config.get_openrouter_api_key()
        .ok_or("OpenRouter API key not found")?;
    
    let agent = RustAgentBuilder::new("my-agent")
        .with_openrouter("google/gemini-2.5-pro", api_key)
        .build()
        .map_err(|e| format!("Failed to build agent: {}", e))?;
    
    Ok(agent)
}
```

### Memory Management

The library handles memory management automatically:

- **Agents**: Automatically destroyed when dropped
- **Conversations**: Proper cleanup of C# resources
- **Strings**: Safe FFI string handling with proper allocation/deallocation
- **Plugins**: Automatic registration and cleanup

## 🧪 Testing

Run all tests:

```bash
cargo test
```

Run specific module tests:

```bash
cargo test test_module1  # FFI Bridge tests
cargo test test_module2  # Object Lifecycle tests
cargo test test_module3  # Streaming tests
cargo test test_module4  # Plugin System tests
cargo test test_module5  # Ergonomic Plugin tests
```

Run with output:

```bash
cargo test -- --nocapture
```

## 🚧 Platform Support

- **Windows**: Full support with .NET Framework and .NET Core
- **macOS**: Full support with .NET Core
- **Linux**: Full support with .NET Core

### Build Requirements

- Rust 1.70+
- .NET 8.0+
- C# HPD Agent framework

### Cross-compilation

```bash
# For Windows
cargo build --target x86_64-pc-windows-msvc

# For macOS
cargo build --target x86_64-apple-darwin

# For Linux
cargo build --target x86_64-unknown-linux-gnu
```

## Building from Source

### Prerequisites

- [Rust](https://rustup.rs/) (latest stable)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Quick Start

1. **Build the native C# library:**
   ```bash
   ./build-native.sh
   ```
   
   Or for a specific platform:
   ```bash
   ./build-native.sh win-x64     # Windows x64
   ./build-native.sh osx-arm64   # macOS Apple Silicon
   ./build-native.sh osx-x64     # macOS Intel
   ./build-native.sh linux-x64   # Linux x64
   ./build-native.sh linux-arm64 # Linux ARM64
   ```

2. **Build and test the Rust library:**
   ```bash
   cargo test
   ```

### Manual Build Process

If you prefer to build manually:

1. **Build the C# native library:**
   ```bash
   cd ../../HPD-Agent
2. **Copy the library to the Rust project:**
   
   **Windows:**
   ```bash
   cp bin/Release/net9.0/win-x64/publish/HPD-Agent.dll .
   ```
   
   **macOS:**
   ```bash
   cp bin/Release/net9.0/osx-arm64/publish/HPD-Agent.dylib .
   install_name_tool -id @loader_path/HPD-Agent.dylib HPD-Agent.dylib
   ln -sf HPD-Agent.dylib libhpdagent.dylib
   ```
   
   **Linux:**
   ```bash
   cp bin/Release/net9.0/linux-x64/publish/libHPD-Agent.so .
   ```

3. **Test the Rust library:**
   ```bash
   cargo test
   ```

## Platform-Specific Notes

### Windows
- Library extension: `.dll`
- No special symlinks required
- Requires Visual Studio C++ redistributables on target machines

### macOS
- Library extension: `.dylib`
- Requires symlink `libhpdagent.dylib` for Rust linker
- Install name fixed to use `@loader_path` for portability

### Linux
- Library extension: `.so`
- Library already has `lib` prefix
- May require additional system dependencies

## 🔐 Security

### Permission System

Functions marked with `#[requires_permission]` will trigger the permission system on the C# side before execution. This allows users to approve or deny dangerous operations.

### Memory Safety

- All FFI operations are memory-safe
- Proper string allocation and deallocation
- No unsafe operations exposed to plugin developers
- Panic handling in FFI boundaries

### API Key Management

- Store API keys in `appsettings.json` (excluded from version control)
- Environment variable support
- No hardcoded secrets in code

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

1. Clone the repository
2. Install Rust 1.70+
3. Install .NET 8.0+
4. Create `appsettings.json` with your API keys
5. Run tests: `cargo test`

### Code Style

- Follow Rust standard formatting: `cargo fmt`
- Check lints: `cargo clippy`
- Add tests for new features
- Update documentation

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Microsoft.Extensions.AI for conversation types
- OpenRouter for AI model access
- The Rust community for excellent FFI support
- Tokio for async runtime support

## 📞 Support

- 📖 [Documentation](https://docs.rs/hpd_rust_agent)
- 🐛 [Issue Tracker](https://github.com/Ewoofcoding/HPD-Agent/issues)
- 💬 [Discussions](https://github.com/Ewoofcoding/HPD-Agent/discussions)

---

Made with ❤️ by the HPD Agent team

```
┌─────────────┐    FFI     ┌──────────────────┐
│             │◄──────────►│                  │
│ Rust Client │            │ C# Agent Library │
│             │            │ (.NET 9.0)       │
└─────────────┘            └──────────────────┘
```

## Testing

Run the FFI bridge test to verify everything is working:

```bash
cargo test it_pings_csharp
```

This test verifies:
- ✅ Cross-language function calls work
- ✅ String marshaling works correctly  
- ✅ Memory management is proper
- ✅ Library loading succeeds

## Development

The project structure:

```
hpd_rust_agent/
├── build-native.sh         # Cross-platform build script
├── build.rs                # Rust build configuration
├── Cargo.toml             # Rust dependencies
├── src/
│   ├── lib.rs             # Main library and tests
│   └── ffi.rs             # FFI bindings (platform-aware)
└── HPD-Agent.{dylib|dll|so} # Native library (platform-specific)
```

## Troubleshooting

### Library Not Found
- Ensure you've run `./build-native.sh` successfully
- Check that the native library exists in the current directory
- On macOS, verify the symlink `libhpdagent.dylib` exists

### Test Failures
- Verify the C# project builds without errors
- Check that you're using the correct Runtime Identifier (RID) for your platform
- Ensure .NET 9.0 SDK is installed

### Cross-Compilation
- The Rust code can be cross-compiled, but you'll need the appropriate C# native library for each target platform
- Use the `build-native.sh` script with the target platform RID
