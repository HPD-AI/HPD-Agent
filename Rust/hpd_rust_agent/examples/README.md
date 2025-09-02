# Examples

This directory contains comprehensive examples demonstrating the capabilities of the HPD Rust Agent Library.

## Quick Start Examples

### [Basic Agent](basic_agent.rs)
Simple agent creation and conversation example.

### [Plugin Development](plugin_development.rs)
Complete guide to creating plugins with the ergonomic macro system.

### [Streaming Conversations](streaming_example.rs)
Real-time streaming with event handling.

### [Multi-Agent Setup](multi_agent.rs)
Managing conversations with multiple AI agents.

## Advanced Examples

### [File Operations Plugin](advanced_plugins/file_operations.rs)
Safe file operations with permission system.

### [Web Search Plugin](advanced_plugins/web_search.rs)
HTTP requests and web content processing.

### [Database Plugin](advanced_plugins/database.rs)
Database operations with connection pooling.

### [Custom Error Handling](error_handling.rs)
Comprehensive error handling patterns.

### [Configuration Management](configuration.rs)
Advanced configuration with environment variables.

## Platform-Specific Examples

### [Windows Integration](platform/windows.rs)
Windows-specific features and COM integration.

### [macOS Integration](platform/macos.rs)
macOS frameworks and native integration.

### [Linux Integration](platform/linux.rs)
Linux system calls and D-Bus integration.

## Integration Examples

### [C# Interop](integration/csharp_integration.rs)
Advanced FFI patterns and memory management.

### [WASM Target](integration/wasm_example.rs)
WebAssembly compilation and browser integration.

### [Performance Testing](integration/benchmarks.rs)
Performance benchmarks and optimization examples.

## Running Examples

Each example can be run independently:

```bash
# Basic examples
cargo run --example basic_agent
cargo run --example plugin_development
cargo run --example streaming_example

# Advanced examples
cargo run --example file_operations
cargo run --example web_search

# Integration examples
cargo run --example csharp_integration
```

## Configuration

Examples that require API access need `appsettings.json`:

```json
{
  "OpenRouterApiKey": "your-api-key-here",
  "DefaultModel": "google/gemini-2.5-pro",
  "DefaultInstructions": "You are a helpful AI assistant."
}
```

## Example Categories

- 🚀 **Quick Start**: Get up and running quickly
- 🔧 **Plugin Development**: Learn the plugin system
- 📡 **Streaming**: Real-time communication patterns
- 🏗️ **Advanced**: Complex use cases and patterns
- 🌐 **Platform**: Platform-specific integrations
- 🔗 **Integration**: Language and system integration
