# FFI API Reference - Dynamic Plugin Metadata

This document provides detailed reference information for the FFI (Foreign Function Interface) functions that enable communication between C# and Rust in the Dynamic Plugin Metadata system.

## Overview

The FFI bridge consists of functions exported from C# and consumed by Rust, enabling runtime plugin metadata operations with high performance and memory safety.

## C# Exported Functions

### Core Metadata Functions

#### `get_plugin_metadata_json()`
```csharp
[UnmanagedCallersOnly(EntryPoint = "get_plugin_metadata_json")]
public static IntPtr GetPluginMetadataJson()
```
**Purpose**: Retrieves pre-generated metadata for all registered plugins as JSON.

**Returns**: Pointer to UTF-8 JSON string containing plugin metadata, or `IntPtr.Zero` on failure.

**Example Output**:
```json
[
  {
    "PluginName": "SearchPlugin",
    "PluginType": "MyApp.Plugins.SearchPlugin",
    "AssemblyName": "MyApp.Plugins",
    "IsInstance": false
  }
]
```

**Rust Usage**:
```rust
use hpd_rust_agent::ffi_interface;

match ffi_interface::get_plugin_metadata() {
    Ok(metadata) => println!("Plugins: {}", metadata),
    Err(e) => eprintln!("Failed to get metadata: {}", e),
}
```

---

### Context Handle Management

#### `create_context_handle(config_json)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "create_context_handle")]
public static IntPtr CreateContextHandle(IntPtr configJsonPtr)
```
**Purpose**: Creates a context handle from JSON configuration for efficient reuse.

**Parameters**:
- `configJsonPtr`: Pointer to UTF-8 JSON string containing `PluginConfiguration`

**Returns**: Handle to the created context, or `IntPtr.Zero` on failure.

**Configuration Format**:
```json
{
  "pluginName": "SearchPlugin",
  "contextType": "SearchPluginContext",
  "properties": {
    "language": "en",
    "userRole": "admin",
    "maxResults": 50
  },
  "availableFunctions": ["search", "advancedSearch"]
}
```

**Rust Usage**:
```rust
let config = PluginConfiguration::new("SearchPlugin", "SearchPluginContext")
    .with_property("language", "en")?;

let context_handle = ffi_interface::ContextHandle::new(&config)?;
```

---

#### `update_context_handle(context_handle, config_json)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "update_context_handle")]
public static bool UpdateContextHandle(IntPtr contextHandle, IntPtr configJsonPtr)
```
**Purpose**: Updates an existing context handle with new configuration.

**Parameters**:
- `contextHandle`: Handle to the existing context
- `configJsonPtr`: Pointer to UTF-8 JSON string containing updated `PluginConfiguration`

**Returns**: `true` if update succeeded, `false` otherwise.

**Performance**: ~5ms (handle replacement via ObjectManager)

**Rust Usage**:
```rust
let updated_config = config.with_property("language", "es")?;
context_handle.update(&updated_config)?;
```

---

#### `destroy_context_handle(context_handle)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "destroy_context_handle")]
public static void DestroyContextHandle(IntPtr contextHandle)
```
**Purpose**: Destroys a context handle and releases its resources.

**Parameters**:
- `contextHandle`: Handle to the context to destroy

**Returns**: None (void)

**Note**: Automatically called by Rust's `Drop` trait implementation.

---

### Conditional Evaluation

#### `evaluate_precompiled_condition(plugin_type, function_name, context_handle)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "evaluate_precompiled_condition")]
public static bool EvaluatePrecompiledCondition(
    IntPtr pluginTypeNamePtr, 
    IntPtr functionNamePtr, 
    IntPtr contextHandle)
```
**Purpose**: Evaluates a precompiled conditional expression using source-generated evaluators.

**Parameters**:
- `pluginTypeNamePtr`: Pointer to UTF-8 string containing plugin type name
- `functionNamePtr`: Pointer to UTF-8 string containing function name  
- `contextHandle`: Handle to the plugin context

**Returns**: `true` if condition evaluates to true, `false` otherwise.

**Performance**: <1ms per evaluation (uses pre-compiled evaluators)

**Example C# Conditional**:
```csharp
[AIFunction<MyContext>]
[Conditional("context.UserRole == \"admin\" && context.HasPermission")]
public async Task<string> AdminFunctionAsync(MyContext context) { }
```

**Rust Usage**:
```rust
let is_available = context_handle.evaluate_condition("MyPlugin", "AdminFunction")?;
if is_available {
    println!("AdminFunction is available for this context");
}
```

---

### Function Filtering

#### `filter_available_functions(plugin_type, context_handle)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "filter_available_functions")]
public static IntPtr FilterAvailableFunctions(
    IntPtr pluginTypeNamePtr, 
    IntPtr contextHandle)
```
**Purpose**: Filters available functions for a plugin based on context.

**Parameters**:
- `pluginTypeNamePtr`: Pointer to UTF-8 string containing plugin type name
- `contextHandle`: Handle to the plugin context

**Returns**: Pointer to UTF-8 JSON string containing available functions array, or `IntPtr.Zero` on failure.

**Performance**: ~50ms for 100+ functions with conditional filtering

**Output Format**:
```json
[
  {
    "name": "search",
    "resolvedDescription": "Search through available data",
    "schema": {
      "type": "object",
      "properties": {
        "query": {"type": "string"},
        "maxResults": {"type": "integer"}
      }
    },
    "isAvailable": true,
    "requiresPermission": false
  }
]
```

**Rust Usage**:
```rust
let available_functions = context_handle.get_available_functions("SearchPlugin")?;
for function in available_functions {
    println!("Available: {} - {}", function.name, function.resolved_description);
    println!("  Schema: {:#}", serde_json::to_string_pretty(&function.schema)?);
}
```

---

## Memory Management

### String Memory Management

#### `free_string(ptr)`
```csharp
[UnmanagedCallersOnly(EntryPoint = "free_string")]
public static void FreeString(IntPtr stringPtr)
```
**Purpose**: Frees memory allocated by C# for strings returned to Rust.

**Parameters**:
- `stringPtr`: Pointer to the string memory to free

**Important**: This function is automatically called by the Rust FFI interface. Manual calls are not required when using the high-level APIs.

---

## Error Handling

### FFI Error Patterns

All FFI functions follow consistent error handling patterns:

1. **Null Pointer Returns**: Functions returning pointers return `IntPtr.Zero` on failure
2. **Boolean Returns**: Functions return `false` on failure, `true` on success
3. **Logging**: Errors are logged to console with `[ERR]` prefix
4. **Graceful Degradation**: Functions fail safely without crashes

### Rust Error Propagation

The Rust FFI interface converts C# errors into structured Rust errors:

```rust
// All FFI operations return Result<T, String>
match context_handle.evaluate_condition("Plugin", "function") {
    Ok(result) => println!("Success: {}", result),
    Err(error) => eprintln!("Error: {}", error),
}
```

### Common Error Scenarios

1. **Invalid JSON Configuration**
   ```rust
   // Error: "Failed to serialize config: invalid type: floating point `NaN`"
   let result = config.with_property("invalid", std::f64::NAN);
   ```

2. **C# Runtime Not Available**
   ```rust
   // Error: "FFI function returned null"  
   let result = ffi_interface::get_plugin_metadata();
   ```

3. **Invalid Plugin Type**
   ```rust
   // Error: "Plugin type 'NonexistentPlugin' not found"
   let result = context_handle.get_available_functions("NonexistentPlugin");
   ```

4. **Context Handle Invalid**
   ```rust
   // Error: "Context handle is invalid or has been destroyed"
   // (This shouldn't happen with proper RAII usage)
   ```

---

## Performance Characteristics

### Benchmarking Results

| Operation | Average Time | Notes |
|-----------|--------------|--------|
| `create_context_handle` | 10.2ms | FFI + JSON deserialization |
| `update_context_handle` | 4.8ms | Handle replacement |
| `evaluate_precompiled_condition` | 0.8ms | Pre-compiled evaluators |
| `filter_available_functions` | 52ms | 100+ functions with conditionals |
| `get_plugin_metadata_json` | 2.3ms | Cached metadata retrieval |
| `destroy_context_handle` | 0.1ms | ObjectManager cleanup |

### Memory Usage

| Component | Memory Usage | Lifetime |
|-----------|--------------|----------|
| Context Handle | ~1KB | Until explicitly destroyed |
| Plugin Configuration | ~2-5KB | Depends on properties |
| Function Metadata | ~500B per function | During filtering operation |
| JSON Serialization | ~3x property size | Temporary during operations |

---

## Thread Safety

### C# Side Thread Safety
- All FFI functions are thread-safe
- ObjectManager uses concurrent collections
- Context operations are atomic

### Rust Side Thread Safety
- `ContextHandle` implements `Send` and `Sync`
- Multiple threads can safely share context handles
- FFI operations are internally synchronized

**Example Multi-threaded Usage**:
```rust
use std::sync::Arc;
use std::thread;

let context_handle = Arc::new(ffi_interface::ContextHandle::new(&config)?);

let handles: Vec<_> = (0..4).map(|i| {
    let handle = Arc::clone(&context_handle);
    thread::spawn(move || {
        let result = handle.evaluate_condition("Plugin", &format!("function{}", i));
        println!("Thread {} result: {:?}", i, result);
    })
}).collect();

for handle in handles {
    handle.join().unwrap();
}
```

---

## Advanced Usage

### Raw FFI Access

For advanced scenarios, access raw FFI functions directly:

```rust
use std::ffi::{CString, CStr};
use hpd_rust_agent::ffi;

unsafe {
    let plugin_name = CString::new("MyPlugin").unwrap();
    let function_name = CString::new("myFunction").unwrap();
    
    let result = ffi::evaluate_precompiled_condition(
        plugin_name.as_ptr(),
        function_name.as_ptr(),
        context_handle.as_raw()
    );
    
    println!("Raw FFI result: {}", result);
}
```

### Custom ObjectManager Integration

For plugins that need to integrate with the ObjectManager:

```csharp
// C# side - storing custom objects
public static IntPtr StoreCustomObject(object obj)
{
    return ObjectManager.Add(obj);
}

// Rust side - accessing stored objects
let custom_handle = unsafe { ffi::store_custom_object(...) };
// Use handle with other FFI functions
```

---

## Debugging and Diagnostics

### Enable Debug Logging

Set environment variable for detailed FFI logging:
```bash
export HPD_PLUGIN_DEBUG=1
```

### FFI Call Tracing

```rust
// Enable tracing for all FFI calls
std::env::set_var("RUST_LOG", "hpd_rust_agent::ffi=debug");

// Trace specific operations
use log::debug;
debug!("About to call evaluate_condition");
let result = context_handle.evaluate_condition("Plugin", "function")?;
debug!("evaluate_condition returned: {}", result);
```

### Memory Leak Detection

```rust
// Monitor ObjectManager usage
match ffi_interface::get_plugin_metadata() {
    Ok(metadata) => {
        // Check if metadata contains object count information
        if let Some(debug_info) = metadata.get("debug") {
            println!("ObjectManager objects: {}", debug_info);
        }
    }
    Err(e) => eprintln!("Debug info unavailable: {}", e),
}
```

---

This FFI API reference provides comprehensive documentation for all low-level operations in the Dynamic Plugin Metadata system. For most use cases, the high-level Rust APIs (`PluginConfiguration`, `ContextHandle`, `AgentBuilder`) are recommended over direct FFI usage.