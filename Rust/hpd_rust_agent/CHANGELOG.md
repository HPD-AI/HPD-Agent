# Changelog

All notable changes to the HPD Rust Agent Library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Complete documentation with examples and API reference
- Contributing guidelines and development setup instructions

## [0.1.0] - 2024-09-01

### Added

#### Module 1: FFI Bridge Foundation
- Cross-platform native library compilation support (Windows, macOS, Linux)
- Memory-safe string handling between C# and Rust with proper allocation/deallocation
- Basic FFI function bindings for agent operations
- Ping/pong functionality for testing C# ↔ Rust communication
- Platform-specific library handling (.dll, .dylib, .so)

#### Module 2: Object Lifecycle Management
- `RustAgentBuilder` with fluent API for agent configuration
- Support for OpenRouter and Ollama AI providers
- JSON serialization optimized for .NET AOT compatibility
- `RustAgent` with proper lifecycle management and automatic cleanup
- `RustConversation` for managing multi-agent conversations
- Configuration loading from `appsettings.json`

#### Module 3: Conversation & Streaming
- Real-time streaming conversations with callback-based architecture
- `RustConversation::send()` for synchronous message handling
- `RustConversation::send_streaming()` for async streaming responses
- Microsoft.Extensions.AI integration for event handling
- Proper async message processing with Tokio runtime
- Stream event types: STEP_STARTED, TEXT_MESSAGE_CONTENT, FUNCTION_CALL_STARTED, etc.

#### Module 4: Automated Plugin System
- Global plugin registry with thread-safe registration
- `PluginRegistration` for manual plugin registration
- JSON schema generation for OpenAI function calling compatibility
- `get_registered_plugins()` and `get_plugin_stats()` for plugin discovery
- FFI integration allowing C# to consume Rust plugins
- Runtime plugin function discovery and invocation

#### Module 5: Ergonomic Plugin Development
- `#[hpd_plugin]` procedural macro for zero-boilerplate plugin creation
- `#[ai_function]` for marking methods as AI-callable functions
- `#[requires_permission]` for functions requiring user approval
- Automatic Plugin trait implementation via macros
- Constructor-based auto-registration with `#[ctor]` integration
- Automatic parameter parsing and JSON schema generation
- Support for custom function names and descriptions
- Full feature parity with C# attribute system

#### Core Features
- **Memory Safety**: All FFI operations are memory-safe with proper lifecycle management
- **Error Handling**: Comprehensive error propagation across language boundaries
- **Async Support**: Full async/await support with Tokio runtime integration
- **Security**: Built-in permission system for sensitive operations
- **Performance**: Optimized for .NET AOT compilation and cross-platform deployment
- **Ergonomics**: Zero-boilerplate plugin development with procedural macros

#### Configuration
- `AppSettings` for loading configuration from `appsettings.json`
- Support for OpenRouter API keys and default models
- Environment-based configuration options
- Default instructions and model settings

#### Testing
- Comprehensive test suite covering all 5 modules
- FFI bridge tests with real C# integration
- Plugin system tests with auto-registration validation
- Streaming tests with real API calls
- Memory safety tests for proper cleanup
- Cross-platform compatibility tests

### Technical Details

#### Dependencies
- `tokio` - Async runtime for streaming and async operations
- `serde` + `serde_json` - JSON serialization for .NET interop
- `syn` + `quote` - Procedural macro support for code generation
- `ctor` - Constructor-based auto-registration
- `tokio-stream` - Stream utilities for async message processing

#### FFI Interface
- `ping()` - Basic connectivity testing
- `create_agent()` - Agent creation from JSON configuration
- `create_conversation()` - Conversation management
- `send_message()` - Synchronous message sending
- `send_streaming_message()` - Async streaming message sending
- `destroy_agent()` / `destroy_conversation()` - Resource cleanup
- `free_string()` - Memory management for C# allocated strings

#### Plugin System
- Automatic function discovery and registration
- JSON schema generation for OpenAI function calling
- Permission-based security model
- Thread-safe global registry
- FFI wrapper generation for C# consumption

#### Generated Code Examples
The procedural macros generate optimized code including:
- Plugin registration constructors
- FFI wrapper functions with proper error handling
- JSON schema definitions
- Plugin trait implementations

### Breaking Changes
- Initial release, no breaking changes

### Deprecated
- None

### Removed
- None

### Fixed
- None

### Security
- Implemented permission system for sensitive AI functions
- Memory-safe FFI operations with proper string handling
- No hardcoded API keys or sensitive information

## Development Milestones

### Module 1 Completion
- ✅ Basic FFI bridge with ping/pong functionality
- ✅ Cross-platform native library compilation
- ✅ Memory-safe string handling

### Module 2 Completion  
- ✅ Agent builder pattern with fluent API
- ✅ JSON serialization for .NET AOT compatibility
- ✅ Configuration management from appsettings.json

### Module 3 Completion
- ✅ Real-time streaming conversations
- ✅ Async message processing with proper event handling
- ✅ Microsoft.Extensions.AI integration

### Module 4 Completion
- ✅ Global plugin registry with thread-safe operations
- ✅ JSON schema generation for AI function calling
- ✅ FFI integration for C# plugin consumption

### Module 5 Completion
- ✅ Procedural macros for ergonomic plugin development
- ✅ Zero-boilerplate plugin creation
- ✅ Auto-registration with constructor-based loading
- ✅ Full feature parity with C# attribute system

## Testing Results

### Test Statistics
- **Total Tests**: 17 passing
- **Module 1**: 4 tests (FFI Bridge)
- **Module 2**: 3 tests (Object Lifecycle)
- **Module 3**: 2 tests (Streaming)
- **Module 4**: 4 tests (Plugin System)
- **Module 5**: 4 tests (Ergonomic Plugins)

### Plugin Validation
- **Total Plugins**: 4 (FinalTestPlugin, MathPlugin, StringPlugin, AsyncPlugin)
- **Total Functions**: 24 AI-callable functions
- **Schema Generation**: All functions generate valid OpenAI schemas
- **Auto-Registration**: All plugins successfully auto-register at startup

### Integration Testing
- ✅ C# ↔ Rust FFI communication validated
- ✅ OpenRouter with Google Gemini 2.5 Pro integration confirmed
- ✅ Real-time streaming with proper event handling verified
- ✅ Memory management and resource cleanup validated
- ✅ Cross-module integration proven with comprehensive testing

## Future Roadmap

### Planned Features
- Additional AI provider integrations (Azure OpenAI, Anthropic)
- Plugin marketplace and discovery system
- Advanced streaming with backpressure handling
- WebAssembly (WASM) target support
- Performance optimizations and benchmarking
- Enhanced error reporting and debugging tools

### Community Contributions
- Documentation improvements and examples
- Additional example plugins
- Cross-platform testing and validation
- Performance testing and optimization

---

This changelog follows the principles of [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
