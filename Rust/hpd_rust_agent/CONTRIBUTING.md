# Contributing to HPD Rust Agent Library

Thank you for your interest in contributing to the HPD Rust Agent Library! This document provides guidelines and information for contributors.

## 🚀 Getting Started

### Development Environment Setup

1. **Install Prerequisites:**
   - [Rust](https://rustup.rs/) (latest stable version)
   - [.NET 8.0+ SDK](https://dotnet.microsoft.com/download)
   - A good code editor (VS Code with Rust extension recommended)

2. **Clone the Repository:**
   ```bash
   git clone https://github.com/Ewoofcoding/HPD-Agent.git
   cd HPD-Agent/Rust/hpd_rust_agent
   ```

3. **Set Up Configuration:**
   ```bash
   cp appsettings.json.example appsettings.json
   # Edit appsettings.json with your API keys
   ```

4. **Build and Test:**
   ```bash
   cargo build
   cargo test
   ```

## 🏗️ Project Structure

```
hpd_rust_agent/
├── src/
│   ├── lib.rs              # Main library entry point
│   ├── ffi.rs              # FFI bindings to C#
│   ├── agent.rs            # Agent builder and management
│   ├── conversation.rs     # Conversation handling
│   ├── streaming.rs        # Streaming functionality
│   ├── config.rs           # Configuration management
│   ├── plugins.rs          # Plugin system
│   ├── example_plugins.rs  # Example plugin implementations
│   └── tests/
│       ├── test_module1.rs # FFI Bridge tests
│       ├── test_module2.rs # Object Lifecycle tests
│       ├── test_module3.rs # Streaming tests
│       ├── test_module4.rs # Plugin System tests
│       └── test_module5.rs # Ergonomic Plugin tests
├── hpd_rust_agent_macros/  # Procedural macros crate
│   └── src/
│       └── lib.rs          # Macro implementations
├── Cargo.toml              # Dependencies and metadata
├── README.md               # Main documentation
└── appsettings.json        # Configuration (not in git)
```

## 🛠️ Development Guidelines

### Code Style

We follow standard Rust conventions:

- **Formatting**: Use `cargo fmt` to format code
- **Linting**: Use `cargo clippy` to catch common issues
- **Naming**: Use `snake_case` for functions/variables, `PascalCase` for types
- **Documentation**: Document all public APIs with `///` comments

### Testing

All contributions must include appropriate tests:

```bash
# Run all tests
cargo test

# Run specific test modules
cargo test test_module1
cargo test test_module5

# Run tests with output
cargo test -- --nocapture

# Run tests with API calls (requires configuration)
cargo test it_sends_and_receives_a_message -- --nocapture
```

### Module Guidelines

#### Module 1: FFI Bridge Foundation
- Focus on memory safety
- Ensure proper string handling
- Test cross-platform compatibility

#### Module 2: Object Lifecycle Management
- Validate JSON serialization
- Test agent creation and destruction
- Ensure proper resource cleanup

#### Module 3: Conversation & Streaming
- Test async functionality thoroughly
- Validate streaming event handling
- Ensure proper error propagation

#### Module 4: Automated Plugin System
- Test plugin registration and discovery
- Validate JSON schema generation
- Ensure FFI integration works

#### Module 5: Ergonomic Plugin Development
- Focus on macro usability
- Test code generation quality
- Ensure compilation errors are clear

## 🔧 Adding New Features

### Adding a New Plugin Function

1. **Create the function in your plugin:**
   ```rust
   #[ai_function("Description of what the function does")]
   pub async fn new_function(&self, param1: String, param2: i32) -> String {
       // Implementation
   }
   ```

2. **Add tests:**
   ```rust
   #[tokio::test]
   async fn test_new_function() {
       let plugin = MyPlugin::default();
       let result = plugin.new_function("test".to_string(), 42).await;
       assert!(!result.is_empty());
   }
   ```

3. **Update documentation in the function comment**

### Adding a New Macro Feature

1. **Implement in `hpd_rust_agent_macros/src/lib.rs`**
2. **Add parsing for new syntax**
3. **Generate appropriate code**
4. **Add comprehensive tests**
5. **Update macro documentation**

### Adding FFI Functions

1. **Add to `src/ffi.rs`:**
   ```rust
   extern "C" {
       pub fn new_ffi_function(param: *const c_char) -> *mut c_char;
   }
   ```

2. **Add safe wrapper:**
   ```rust
   pub fn safe_new_function(input: &str) -> Result<String, String> {
       // Safe wrapper implementation
   }
   ```

3. **Add tests for both safe and unsafe variants**

## 🧪 Testing Strategy

### Unit Tests
- Test individual functions in isolation
- Mock external dependencies where possible
- Focus on edge cases and error conditions

### Integration Tests
- Test complete workflows
- Use real API calls in CI/CD (with test accounts)
- Validate cross-module interactions

### FFI Tests
- Test memory safety
- Validate string handling
- Test error propagation across language boundaries

### Macro Tests
- Test code generation
- Validate compilation of generated code
- Test error messages for invalid syntax

## 📝 Pull Request Process

1. **Fork the repository**
2. **Create a feature branch:**
   ```bash
   git checkout -b feature/my-new-feature
   ```

3. **Make your changes:**
   - Write code following our guidelines
   - Add tests for new functionality
   - Update documentation as needed

4. **Test thoroughly:**
   ```bash
   cargo fmt
   cargo clippy
   cargo test
   ```

5. **Commit with clear messages:**
   ```bash
   git commit -m "feat: add new AI function for file operations"
   ```

6. **Push and create PR:**
   ```bash
   git push origin feature/my-new-feature
   ```

### PR Requirements

- [ ] All tests pass
- [ ] Code is formatted with `cargo fmt`
- [ ] No warnings from `cargo clippy`
- [ ] Documentation is updated
- [ ] Appropriate tests are included
- [ ] Commit messages follow conventional commits
- [ ] Changes are described in PR description

## 🐛 Bug Reports

### Before Reporting

1. Check existing issues
2. Ensure you're using the latest version
3. Test with minimal reproduction case

### Bug Report Template

```markdown
**Describe the bug**
A clear description of what the bug is.

**To Reproduce**
Steps to reproduce the behavior:
1. 
2. 
3. 

**Expected behavior**
What you expected to happen.

**Environment:**
- OS: [e.g., Windows 11, macOS 14.0, Ubuntu 22.04]
- Rust version: [e.g., 1.70.0]
- .NET version: [e.g., 8.0.1]

**Additional context**
Any other context about the problem.
```

## ✨ Feature Requests

### Feature Request Template

```markdown
**Is your feature request related to a problem?**
A clear description of what the problem is.

**Describe the solution you'd like**
A clear description of what you want to happen.

**Describe alternatives you've considered**
Other solutions you've considered.

**Additional context**
Any other context about the feature request.
```

## 🚀 Release Process

### Version Numbering

We follow [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Checklist

- [ ] All tests pass on all supported platforms
- [ ] Documentation is updated
- [ ] CHANGELOG.md is updated
- [ ] Version numbers are bumped in Cargo.toml
- [ ] Git tag is created
- [ ] Release notes are written

## 📚 Documentation

### API Documentation

- Use `///` for public APIs
- Include examples in doc comments
- Run `cargo doc` to generate docs

### Example Documentation

```rust
/// Creates a new AI agent with the specified configuration.
/// 
/// # Arguments
/// 
/// * `name` - A unique identifier for the agent
/// * `instructions` - The system instructions for the agent
/// 
/// # Returns
/// 
/// Returns a `Result` containing the built `RustAgent` or an error string.
/// 
/// # Examples
/// 
/// ```rust
/// use hpd_rust_agent::RustAgentBuilder;
/// 
/// let agent = RustAgentBuilder::new("my-agent")
///     .with_instructions("You are a helpful assistant")
///     .build()?;
/// ```
pub fn build(self) -> Result<RustAgent, String> {
    // Implementation
}
```

## 🤝 Community

### Code of Conduct

We are committed to providing a welcoming and inclusive experience for everyone. Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

### Getting Help

- 📖 Read the [documentation](README.md)
- 🐛 Check [existing issues](https://github.com/Ewoofcoding/HPD-Agent/issues)
- 💬 Start a [discussion](https://github.com/Ewoofcoding/HPD-Agent/discussions)
- 📧 Contact the maintainers

### Recognition

Contributors will be recognized in:
- README.md acknowledgments
- Release notes for significant contributions
- GitHub contributor graphs

Thank you for contributing to HPD Rust Agent Library! 🎉
