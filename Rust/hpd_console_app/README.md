# HPD Console Application

A standalone console application that uses the `hpd_rust_agent` library as an external dependency. This serves as a real-world test of the library's API and demonstrates how external applications would consume it.

## Features

- **Interactive Chat Mode**: Have real-time conversations with AI agents
- **Plugin System Testing**: Demonstrates external plugin registration and usage
- **Multiple Commands**: Test different aspects of the library
- **Colored Output**: Enhanced user experience with colored console output
- **Error Handling**: Robust error handling and user feedback

## Usage

### Prerequisites

1. Make sure you have the HPD Rust Agent library built
2. Copy your OpenRouter API key to `appsettings.json`

### Building

```bash
cd hpd_console_app
cargo build
```

### Running

#### Interactive Chat Mode
```bash
cargo run -- chat
cargo run -- chat --agent "coding-assistant" --instructions "You are an expert programmer"
cargo run -- chat --max-calls 10
```

#### Run Basic Tests
```bash
cargo run -- test
```

#### Show Plugin Information
```bash
cargo run -- plugins
```

#### Run Demo Conversation
```bash
cargo run -- demo
```

### Help
```bash
cargo run -- --help
```

## Configuration

Edit `appsettings.json` to configure:
- OpenRouter API credentials
- Default model selection
- Agent behavior settings
- Console appearance options

## Plugin System

The console app includes a sample plugin (`ConsoleUtilities`) that demonstrates:
- Time functions
- Math calculations  
- Text styling utilities

This shows how external applications can extend the library with their own plugins.

## Testing the Library

This console application serves as a comprehensive test of the library's public API:

1. **Configuration Loading**: Tests `AppSettings::load()`
2. **Agent Creation**: Tests `AgentBuilder` fluent API
3. **Conversation Management**: Tests `Conversation::new()` and messaging
4. **Plugin Registration**: Tests the plugin system
5. **Error Handling**: Tests error propagation and handling

## Architecture

```
Console App
    ├── CLI Interface (clap)
    ├── Interactive Chat
    ├── Demo System
    ├── Plugin Testing
    └── Library Integration
            │
            └── hpd_rust_agent (external dependency)
                ├── Agent Management
                ├── Conversation System
                ├── Plugin Framework
                └── Configuration
```

This demonstrates real-world library consumption patterns and validates the library's design.
