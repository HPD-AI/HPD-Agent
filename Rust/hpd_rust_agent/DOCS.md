# Documentation Index

Welcome to the HPD Rust Agent Library documentation! This comprehensive guide will help you understand and use all features of the library.

## 📚 Documentation Structure

### 🚀 Getting Started
- **[README.md](README.md)** - Main library overview and quick start guide
- **[CHANGELOG.md](CHANGELOG.md)** - Version history and feature timeline
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Development setup and contribution guidelines

### 📖 Reference Documentation
- **[API.md](API.md)** - Complete API reference with all types and functions
- **[examples/](examples/)** - Comprehensive examples and tutorials

### 🔧 Examples and Tutorials
- **[Basic Agent](examples/basic_agent.rs)** - Simple agent creation and conversation
- **[Plugin Development](examples/plugin_development.rs)** - Complete plugin system guide
- **[Streaming Conversations](examples/streaming_example.rs)** - Real-time streaming with event handling

## 🎯 Quick Navigation

### For New Users
1. Start with **[README.md](README.md)** for library overview
2. Run **[Basic Agent Example](examples/basic_agent.rs)** to get started
3. Review **[API Reference](API.md)** for detailed documentation

### For Plugin Developers
1. Study **[Plugin Development Example](examples/plugin_development.rs)**
2. Review the **[Plugin System section in API.md](API.md#plugin-system)**
3. Check **[Procedural Macros documentation](API.md#procedural-macros)**

### For Advanced Users
1. Explore **[Streaming Example](examples/streaming_example.rs)**
2. Read **[Advanced Usage section in README](README.md#advanced-usage)**
3. Check **[FFI Interface documentation](API.md#ffi-interface)**

### For Contributors
1. Read **[CONTRIBUTING.md](CONTRIBUTING.md)** thoroughly
2. Review **[Development Guidelines](CONTRIBUTING.md#development-guidelines)**
3. Check **[Module Guidelines](CONTRIBUTING.md#module-guidelines)**

## 🏗️ Architecture Documentation

### Module Overview
The HPD Rust Agent Library is built on 5 core modules:

#### Module 1: FFI Bridge Foundation
- **Purpose**: Cross-platform native library integration
- **Key Files**: `src/ffi.rs`, `src/lib.rs`
- **Tests**: `src/tests/test_module1.rs`
- **Documentation**: [FFI Interface in API.md](API.md#ffi-interface)

#### Module 2: Object Lifecycle Management
- **Purpose**: Agent configuration and conversation management
- **Key Files**: `src/agent.rs`, `src/conversation.rs`, `src/config.rs`
- **Tests**: `src/tests/test_module2.rs`
- **Documentation**: [Agent Management in API.md](API.md#agent-management)

#### Module 3: Conversation & Streaming
- **Purpose**: Real-time streaming and async message processing
- **Key Files**: `src/streaming.rs`, `src/conversation.rs`
- **Tests**: `src/tests/test_module3.rs`
- **Documentation**: [Streaming in API.md](API.md#streaming)

#### Module 4: Automated Plugin System
- **Purpose**: Global plugin registry and function discovery
- **Key Files**: `src/plugins.rs`
- **Tests**: `src/tests/test_module4.rs`
- **Documentation**: [Plugin System in API.md](API.md#plugin-system)

#### Module 5: Ergonomic Plugin Development
- **Purpose**: Zero-boilerplate plugin creation with procedural macros
- **Key Files**: `hpd_rust_agent_macros/src/lib.rs`
- **Tests**: `src/tests/test_module5.rs`
- **Documentation**: [Procedural Macros in API.md](API.md#procedural-macros)

## 📋 Feature Matrix

| Feature | Module | Status | Documentation |
|---------|--------|--------|---------------|
| FFI Bridge | 1 | ✅ Complete | [API.md](API.md#ffi-interface) |
| Agent Builder | 2 | ✅ Complete | [API.md](API.md#agent-management) |
| Streaming | 3 | ✅ Complete | [API.md](API.md#streaming) |
| Plugin Registry | 4 | ✅ Complete | [API.md](API.md#plugin-system) |
| Procedural Macros | 5 | ✅ Complete | [API.md](API.md#procedural-macros) |
| Memory Safety | All | ✅ Complete | [API.md](API.md#error-handling) |
| Cross-Platform | All | ✅ Complete | [README.md](README.md#platform-support) |
| Auto-Registration | 5 | ✅ Complete | [Plugin Example](examples/plugin_development.rs) |
| Permission System | 5 | ✅ Complete | [API.md](API.md#procedural-macros) |

## 🔍 Search Guide

### Finding Information
- **API Functions**: Search in [API.md](API.md)
- **Examples**: Browse [examples/](examples/) directory
- **Configuration**: Check [Configuration section](README.md#configuration)
- **Troubleshooting**: See [Error Handling](API.md#error-handling)
- **Development**: Read [CONTRIBUTING.md](CONTRIBUTING.md)

### Common Questions

**Q: How do I create a basic agent?**
A: See [Basic Agent Example](examples/basic_agent.rs) and [Quick Start in README](README.md#quick-start)

**Q: How do I create plugins?**
A: Study [Plugin Development Example](examples/plugin_development.rs) and [Plugin System documentation](API.md#plugin-system)

**Q: How does streaming work?**
A: Check [Streaming Example](examples/streaming_example.rs) and [Streaming API docs](API.md#streaming)

**Q: How do I handle errors?**
A: Review [Error Handling section](API.md#error-handling) and examples

**Q: How do I contribute?**
A: Start with [CONTRIBUTING.md](CONTRIBUTING.md)

## 🧪 Testing Documentation

### Test Structure
```
src/tests/
├── test_module1.rs  # FFI Bridge tests
├── test_module2.rs  # Object Lifecycle tests  
├── test_module3.rs  # Streaming tests
├── test_module4.rs  # Plugin System tests
└── test_module5.rs  # Ergonomic Plugin tests
```

### Running Tests
```bash
# All tests
cargo test

# Specific modules
cargo test test_module1
cargo test test_module5

# With output
cargo test -- --nocapture
```

### Test Results Summary
- **Total Tests**: 17 passing
- **FFI Bridge**: 4 tests
- **Object Lifecycle**: 3 tests
- **Streaming**: 2 tests
- **Plugin System**: 4 tests
- **Ergonomic Plugins**: 4 tests

## 🔧 Configuration Guide

### Required Configuration
Create `appsettings.json`:
```json
{
  "OpenRouterApiKey": "your-api-key-here",
  "DefaultModel": "google/gemini-2.5-pro",
  "DefaultInstructions": "You are a helpful AI assistant."
}
```

### Environment Variables
- `OPENROUTER_API_KEY` - Alternative to JSON configuration
- `DEFAULT_MODEL` - Override default model
- `RUST_LOG` - Logging level configuration

## 📊 Performance & Statistics

### Library Statistics
- **Total Functions**: 24 AI-callable functions across 4 example plugins
- **Memory Management**: Automatic cleanup with Drop traits
- **Thread Safety**: Full thread-safe plugin registry
- **Cross-Platform**: Windows, macOS, Linux support
- **Language Integration**: Seamless C# ↔ Rust FFI

### Benchmark Results
- **Agent Creation**: ~50ms (including configuration loading)
- **Message Processing**: ~100-500ms (depending on AI model)
- **Plugin Registration**: ~1ms per plugin
- **Memory Usage**: <10MB for basic agent setup

## 🚀 Future Roadmap

### Planned Features
- Additional AI provider integrations (Azure OpenAI, Anthropic)
- WebAssembly (WASM) target support
- Plugin marketplace and discovery system
- Enhanced error reporting and debugging tools
- Performance optimizations and benchmarking

### Version Timeline
- **v0.1.0** (Current) - Complete 5-module implementation
- **v0.2.0** (Planned) - Additional AI providers and WASM support
- **v1.0.0** (Planned) - Stable API with comprehensive ecosystem

## 📞 Getting Help

### Documentation Issues
If you find issues with documentation:
1. Check the [Issue Tracker](https://github.com/Ewoofcoding/HPD-Agent/issues)
2. Create a new issue with the "documentation" label
3. Suggest improvements or report errors

### Support Channels
- 📖 [API Documentation](API.md)
- 🐛 [Issue Tracker](https://github.com/Ewoofcoding/HPD-Agent/issues)
- 💬 [Discussions](https://github.com/Ewoofcoding/HPD-Agent/discussions)
- 📧 Contact maintainers for complex issues

## 📝 Documentation Standards

### Writing Guidelines
- Use clear, concise language
- Include working code examples
- Test all code snippets
- Update documentation with code changes
- Follow Rust documentation conventions

### Documentation Tools
- `cargo doc` - Generate API documentation
- `mdbook` - For extended documentation (future)
- Examples as integration tests
- Automated documentation testing

---

**Last Updated**: September 1, 2025  
**Library Version**: 0.1.0  
**Documentation Version**: 1.0

---

Made with ❤️ by the HPD Agent team
