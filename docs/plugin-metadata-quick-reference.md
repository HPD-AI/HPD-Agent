# Dynamic Plugin Metadata - Quick Reference

## 🚀 Quick Start

### C# Plugin Setup
```csharp
public class MyContext : IPluginMetadataContext
{
    public string Language { get; set; } = "en";
    public string UserRole { get; set; } = "user";
}

[AIFunction<MyContext>]
[Description("{{context.Language == \"es\" ? \"Buscar\" : \"Search\"}}")]
public async Task<string> SearchAsync(string query, MyContext context) { }
```

### Rust Integration
```rust
let config = PluginConfiguration::new("MyPlugin", "MyContext")
    .with_property("Language", "es")?
    .with_property("UserRole", "admin")?;

let agent = AgentBuilder::new("agent")
    .with_plugin_config("MyPlugin", config)
    .build()?;
```

## 📋 Common Patterns

### Multi-language Support
```csharp
[Description("{{context.Language == \"es\" ? \"Descripción en español\" : 
              context.Language == \"fr\" ? \"Description en français\" : 
              \"English description\"}}")]
```

### Role-based Functions
```csharp
[Conditional("context.UserRole == \"admin\"")]
public async Task<string> AdminOnlyFunction() { }

[Conditional("context.UserRole != \"readonly\"")]
public async Task<string> WriteFunction() { }
```

### Experience-based Descriptions
```csharp
[Description("{{context.ExperienceLevel >= 2 ? \"Advanced search with filters\" : \"Simple search\"}}")]
```

## ⚡ Performance Tips

### Context Handle Reuse
```rust
// Good - reuse handle
let context = ContextHandle::new(&config)?;
let result1 = context.evaluate_condition("Plugin", "func1")?;
let result2 = context.evaluate_condition("Plugin", "func2")?;

// Bad - create new handle each time
let result1 = ContextHandle::new(&config)?.evaluate_condition("Plugin", "func1")?;
```

### Batch Operations
```rust
let functions = context.get_available_functions("Plugin")?;
for func in functions {
    println!("{}: {}", func.name, func.resolved_description);
}
```

## 🔧 Configuration Examples

### Basic Configuration
```rust
PluginConfiguration::new("Plugin", "Context")
    .with_property("setting", "value")?
```

### Complex Configuration
```rust
PluginConfiguration::new("Plugin", "Context")
    .with_property("user", json!({
        "id": "123",
        "role": "admin",
        "preferences": {
            "language": "en",
            "theme": "dark"
        }
    }))?
    .with_property("features", vec!["feature1", "feature2"])?
```

### Multiple Plugins
```rust
AgentBuilder::new("agent")
    .with_plugin_config("SearchPlugin", search_config)
    .with_plugin_config("DataPlugin", data_config)
    .with_plugin_config("ReportPlugin", report_config)
    .build()?
```

## 🎨 Template Syntax

### Conditionals
```csharp
// Simple condition
"{{context.Property == \"value\" ? \"True text\" : \"False text\"}}"

// Complex condition
"{{#if (eq context.Role \"admin\")}}Admin text{{else}}User text{{/if}}"

// Multiple conditions
"{{#if (and (eq context.Role \"admin\") (gt context.Level 2))}}Advanced admin{{/if}}"
```

### Helpers
```csharp
// Equality
"{{#if (eq context.Language \"es\")}}Spanish{{/if}}"

// Comparison
"{{#if (gt context.ExperienceLevel 1)}}Advanced{{/if}}"

// Boolean logic
"{{#if (and context.HasPremium context.IsActive)}}Premium Active{{/if}}"
"{{#if (or (eq context.Role \"admin\") (eq context.Role \"moderator\"))}}Elevated{{/if}}"
```

## 🛠️ Error Handling

### Configuration Errors
```rust
match PluginConfiguration::new("Plugin", "Context")
    .with_property("invalid", std::f64::NAN) {
    Ok(config) => { /* use config */ }
    Err(e) => eprintln!("Config error: {}", e),
}
```

### FFI Errors
```rust
match ffi_interface::ContextHandle::new(&config) {
    Ok(handle) => {
        match handle.evaluate_condition("Plugin", "function") {
            Ok(result) => println!("Available: {}", result),
            Err(e) => eprintln!("Evaluation failed: {}", e),
        }
    }
    Err(e) => eprintln!("Context creation failed: {}", e),
}
```

## 📊 Performance Benchmarks

| Operation | Time | Notes |
|-----------|------|-------|
| Context Creation | ~10ms | One-time setup cost |
| Condition Evaluation | <1ms | Pre-compiled evaluators |
| Function Filtering | ~50ms | 100+ functions |
| Context Update | ~5ms | Handle replacement |

## 🧪 Testing

### Unit Test
```rust
#[test]
fn test_config_serialization() {
    let config = PluginConfiguration::new("Test", "TestContext")
        .with_property("key", "value").unwrap();
    
    let json = config.to_json().unwrap();
    let restored = PluginConfiguration::from_json(&json).unwrap();
    
    assert_eq!(config.plugin_name, restored.plugin_name);
}
```

### Integration Test
```rust
#[tokio::test]
async fn test_plugin_integration() {
    let config = PluginConfiguration::new("TestPlugin", "TestContext")
        .with_property("testMode", true).unwrap();
    
    let agent = AgentBuilder::new("test-agent")
        .with_plugin_config("TestPlugin", config)
        .build()
        .unwrap();
    
    // Test agent with plugin configuration
}
```

## 🐛 Common Issues

### Context Handle Creation Fails
- Check if C# runtime is available: `ffi_interface::get_plugin_metadata()`
- Validate JSON: `config.to_json()`
- Verify context type name matches exactly

### Functions Not Available
- Test individual conditions: `context.evaluate_condition(plugin, function)`
- Check context properties are correctly set
- Verify conditional expressions in C# attributes

### JSON Serialization Errors
- Use serializable types only
- Avoid `NaN`, `Infinity`, functions, or complex objects
- Use `serde_json::json!()` macro for complex data

### Memory Issues
- Use RAII patterns (ContextHandle)
- Don't store raw FFI pointers
- Let Rust ownership manage memory

## 📚 API Quick Reference

### PluginConfiguration
```rust
// Create
PluginConfiguration::new(plugin_name, context_type)

// Add properties
.with_property(name, value)?
.with_available_functions(vec!["func1", "func2"])

// Serialize
.to_json()?
.from_json(json)?
```

### ContextHandle
```rust
// Create
ContextHandle::new(&config)?

// Operations
.evaluate_condition(plugin, function)?
.get_available_functions(plugin)?
.update(&new_config)?
```

### AgentBuilder
```rust
// Add plugins
.with_plugin_config(name, config)
.with_dynamic_plugin_context(name, context_type, properties)
```

## 🌟 Best Practices

1. **Reuse Context Handles** - Create once, use many times
2. **Validate Configurations** - Test JSON serialization before use
3. **Handle Errors Gracefully** - FFI operations may fail
4. **Use Type-Safe Properties** - Leverage Rust's type system
5. **Document Context Properties** - Make it clear what properties plugins expect
6. **Test Without C# Runtime** - Ensure graceful degradation
7. **Monitor Performance** - Profile context operations in production
8. **Use Descriptive Names** - Make plugin and context type names clear

---

📖 **Full Documentation**: [dynamic-plugin-metadata.md](./dynamic-plugin-metadata.md)  
🔗 **Examples**: See `/examples/plugin_metadata_phase2_example.rs`  
🧪 **Tests**: Run `cargo test plugin_context` for test coverage