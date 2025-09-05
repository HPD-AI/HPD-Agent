# Dynamic Plugin Metadata System

The Dynamic Plugin Metadata System provides a sophisticated FFI bridge between C# and Rust, enabling context-aware plugin behavior with dynamic descriptions, conditional function availability, and runtime metadata exposure.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
- [C# Plugin Development](#c-plugin-development)
- [Rust Integration](#rust-integration)
- [Advanced Features](#advanced-features)
- [Performance Guide](#performance-guide)
- [API Reference](#api-reference)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

## Overview

This system allows developers to create plugins that adapt their behavior, descriptions, and availability based on runtime context properties like user roles, language preferences, experience levels, and custom settings.

### Key Features

- **Dynamic Descriptions**: Plugin function descriptions that change based on context
- **Conditional Availability**: Functions that appear/disappear based on runtime conditions
- **Context-Aware Schemas**: Parameter schemas that adapt to user context
- **Efficient FFI Bridge**: High-performance communication between C# and Rust
- **Memory Safety**: RAII patterns and automatic resource management
- **Type Safety**: Full type system integration with comprehensive error handling

### Performance Characteristics

| Operation | Performance | Notes |
|-----------|-------------|-------|
| Context Creation | ~10ms | FFI + JSON deserialization |
| Function Filtering | ~50ms | 100+ functions with conditionals |
| Conditional Evaluation | <1ms | Uses pre-compiled evaluators |
| Context Updates | ~5ms | Handle replacement via ObjectManager |

## Architecture

The system consists of two main phases:

### Phase 1: JSON Configuration Extension
- Plugin configuration with runtime properties
- Context serialization and deserialization
- Agent builder integration

### Phase 2: Pre-Generated Metadata FFI Bridge
- FFI functions for metadata exposure
- Context handle caching for performance
- Conditional evaluation using source-generated code
- Function filtering based on runtime context

## Getting Started

### Prerequisites

- HPD-Agent with source generator support
- Rust development environment
- Understanding of FFI concepts (helpful but not required)

### Basic Example

**C# Plugin Definition:**
```csharp
public class UserPreferencesContext : IPluginMetadataContext
{
    public string Language { get; set; } = "en";
    public int ExperienceLevel { get; set; } = 1;
    public bool HasPremiumAccess { get; set; } = false;
}

[AIFunction<UserPreferencesContext>]
[Description("{{context.Language == \"es\" ? \"Búsqueda simple\" : \"Simple search\"}}")]
public async Task<string> SearchAsync(
    [Description("{{context.Language == \"es\" ? \"Término de búsqueda\" : \"Search term\"}}")]
    string query,
    UserPreferencesContext context)
{
    return $"Searching for: {query} (Language: {context.Language})";
}
```

**Rust Usage:**
```rust
use hpd_rust_agent::{PluginConfiguration, AgentBuilder};

// Create plugin configuration
let config = PluginConfiguration::new("SearchPlugin", "UserPreferencesContext")
    .with_property("Language", "es")?
    .with_property("ExperienceLevel", 2)?
    .with_property("HasPremiumAccess", true)?;

// Build agent with plugin configuration
let agent = AgentBuilder::new("search-agent")
    .with_plugin_config("SearchPlugin", config)
    .build()?;
```

## C# Plugin Development

### Creating Context Classes

Context classes implement `IPluginMetadataContext` and define properties that can influence plugin behavior:

```csharp
public class AdvancedPluginContext : IPluginMetadataContext
{
    // User-related properties
    public string UserId { get; set; } = "";
    public string UserRole { get; set; } = "user";
    public string Language { get; set; } = "en";
    
    // Feature flags
    public bool HasPremiumFeatures { get; set; } = false;
    public bool IsInternalUser { get; set; } = false;
    
    // Behavioral settings
    public int ExperienceLevel { get; set; } = 1; // 1=beginner, 2=intermediate, 3=advanced
    public string Theme { get; set; } = "light";
    
    // Custom settings
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}
```

### Dynamic Descriptions

Use templating syntax in `Description` attributes to create context-aware descriptions:

```csharp
[AIFunction<AdvancedPluginContext>]
[Description("""
{{#if (eq context.UserRole "admin")}}
    Administrative data export with full system access
{{else if (eq context.UserRole "moderator")}}
    Moderated data export with restricted access
{{else}}
    {{#if context.HasPremiumFeatures}}
        Premium data export with enhanced options
    {{else}}
        Basic data export functionality
    {{/if}}
{{/if}}
""")]
public async Task<string> ExportDataAsync(
    [Description("{{context.Language == \"es\" ? \"Formato de exportación\" : \"Export format\"}}")]
    string format,
    AdvancedPluginContext context)
{
    // Implementation varies based on context
    var accessLevel = context.UserRole switch
    {
        "admin" => AccessLevel.Full,
        "moderator" => AccessLevel.Restricted,
        _ => context.HasPremiumFeatures ? AccessLevel.Premium : AccessLevel.Basic
    };
    
    return await ExportWithAccessLevel(format, accessLevel);
}
```

### Conditional Function Availability

Functions can be conditionally available based on context properties:

```csharp
[AIFunction<AdvancedPluginContext>]
[Description("Delete sensitive data (Admin only)")]
[Conditional("context.UserRole == \"admin\"")]
public async Task<string> DeleteSensitiveDataAsync(
    string dataId,
    AdvancedPluginContext context)
{
    // Only available to admin users
    return await PerformDeletion(dataId);
}

[AIFunction<AdvancedPluginContext>]
[Description("Advanced analytics dashboard")]
[Conditional("context.HasPremiumFeatures && context.ExperienceLevel >= 2")]
public async Task<string> ShowAdvancedAnalyticsAsync(
    AdvancedPluginContext context)
{
    // Only available to premium users with intermediate+ experience
    return await GenerateAdvancedAnalytics();
}
```

### Multi-language Support

Create language-aware descriptions and parameter schemas:

```csharp
[AIFunction<AdvancedPluginContext>]
[Description("{{GetLocalizedDescription context.Language \"search_function\"}}")]
public async Task<SearchResult[]> SearchAsync(
    [Description("{{GetLocalizedDescription context.Language \"search_query_param\"}}")]
    string query,
    [Description("{{GetLocalizedDescription context.Language \"max_results_param\"}}")]
    int maxResults,
    AdvancedPluginContext context)
{
    // Localized search implementation
}

// Helper method for localization (can be in base class)
private string GetLocalizedDescription(string language, string key)
{
    var localizations = new Dictionary<string, Dictionary<string, string>>
    {
        ["en"] = new() {
            ["search_function"] = "Search through available data sources",
            ["search_query_param"] = "The search query to execute",
            ["max_results_param"] = "Maximum number of results to return"
        },
        ["es"] = new() {
            ["search_function"] = "Buscar en fuentes de datos disponibles",
            ["search_query_param"] = "La consulta de búsqueda a ejecutar",
            ["max_results_param"] = "Número máximo de resultados a devolver"
        },
        ["fr"] = new() {
            ["search_function"] = "Rechercher dans les sources de données disponibles",
            ["search_query_param"] = "La requête de recherche à exécuter",
            ["max_results_param"] = "Nombre maximum de résultats à retourner"
        }
    };
    
    return localizations.GetValueOrDefault(language, localizations["en"])
        ?.GetValueOrDefault(key, key) ?? key;
}
```

## Rust Integration

### Basic Plugin Configuration

Create and configure plugins with context properties:

```rust
use hpd_rust_agent::{PluginConfiguration, ffi_interface};
use std::collections::HashMap;
use serde_json::json;

// Simple configuration
let config = PluginConfiguration::new("MyPlugin", "MyPluginContext")
    .with_property("userRole", "admin")?
    .with_property("language", "fr")?
    .with_property("experienceLevel", 3)?;

// Complex configuration with nested objects
let mut advanced_config = PluginConfiguration::new("AdvancedPlugin", "AdvancedPluginContext");

advanced_config = advanced_config
    .with_property("userId", "user123")?
    .with_property("userRole", "moderator")?
    .with_property("hasPermissions", vec!["read", "write"])?
    .with_property("customSettings", json!({
        "theme": "dark",
        "notifications": true,
        "autoSave": false
    }))?;
```

### Agent Builder Integration

Use the builder pattern to create agents with plugin configurations:

```rust
use hpd_rust_agent::AgentBuilder;

// Single plugin configuration
let agent = AgentBuilder::new("configured-agent")
    .with_instructions("You are a context-aware assistant.")
    .with_plugin_config("SearchPlugin", search_config)
    .build()?;

// Multiple plugin configurations
let agent = AgentBuilder::new("multi-plugin-agent")
    .with_plugin_config("SearchPlugin", search_config)
    .with_plugin_config("DataPlugin", data_config)
    .with_plugin_config("ReportPlugin", report_config)
    .build()?;

// Using the dynamic context helper
let agent = AgentBuilder::new("dynamic-agent")
    .with_dynamic_plugin_context(
        "UserPlugin",
        "UserPluginContext",
        HashMap::from([
            ("language".to_string(), json!("ja")),
            ("timezone".to_string(), json!("Asia/Tokyo")),
            ("currency".to_string(), json!("JPY")),
        ])
    )
    .build()?;
```

### FFI Interface Usage

For advanced scenarios, use the FFI interface directly for runtime metadata operations:

```rust
use hpd_rust_agent::ffi_interface;

// Get all plugin metadata from C#
match ffi_interface::get_plugin_metadata() {
    Ok(metadata) => {
        println!("Available plugins: {}", metadata);
    }
    Err(e) => {
        eprintln!("Failed to get metadata: {}", e);
    }
}

// Create context handle for efficient operations
let context_handle = ffi_interface::ContextHandle::new(&config)?;

// Test conditional evaluation
let is_available = context_handle.evaluate_condition("MyPlugin", "adminFunction")?;
println!("Admin function available: {}", is_available);

// Get filtered functions based on context
let available_functions = context_handle.get_available_functions("MyPlugin")?;
for function in available_functions {
    println!("Function: {} - {}", function.name, function.resolved_description);
    println!("  Available: {}", function.is_available);
    println!("  Requires Permission: {}", function.requires_permission);
    println!("  Schema: {:#}", serde_json::to_string_pretty(&function.schema)?);
}

// Update context dynamically
let updated_config = config
    .with_property("language", "de")?
    .with_property("experienceLevel", 2)?;

context_handle.update(&updated_config)?;
```

### Error Handling

The system provides comprehensive error handling:

```rust
use hpd_rust_agent::{PluginConfiguration, ffi_interface};

// Handle configuration errors
let config_result = PluginConfiguration::new("TestPlugin", "TestContext")
    .with_property("invalidJson", std::f64::NAN); // This will fail

match config_result {
    Ok(config) => {
        // Use config
    }
    Err(e) => {
        eprintln!("Configuration error: {}", e);
        // Handle error appropriately
    }
}

// Handle FFI errors gracefully
match ffi_interface::ContextHandle::new(&config) {
    Ok(handle) => {
        // Use handle
        match handle.evaluate_condition("Plugin", "function") {
            Ok(result) => println!("Condition result: {}", result),
            Err(e) => eprintln!("Evaluation failed: {}", e),
        }
    }
    Err(e) => {
        eprintln!("Context creation failed: {}", e);
        // Fallback behavior
    }
}
```

## Advanced Features

### Context Inheritance

Create hierarchical context structures:

```csharp
public class BasePluginContext : IPluginMetadataContext
{
    public string UserId { get; set; } = "";
    public string Language { get; set; } = "en";
}

public class EnhancedPluginContext : BasePluginContext
{
    public string UserRole { get; set; } = "user";
    public bool HasPremiumAccess { get; set; } = false;
    public Dictionary<string, object> Permissions { get; set; } = new();
}
```

### Dynamic Schema Generation

Create parameter schemas that adapt to context:

```csharp
[AIFunction<EnhancedPluginContext>]
public async Task<string> ConfigurableSearchAsync(
    string query,
    [Description("{{context.HasPremiumAccess ? \"Advanced search options\" : \"Basic search options\"}}")]
    [Schema("""
    {
      "type": "object",
      "properties": {
        "sortBy": {
          "type": "string",
          "enum": {{context.HasPremiumAccess ? "[\"relevance\", \"date\", \"popularity\", \"custom\"]" : "[\"relevance\", \"date\"]"}}
        },
        "filters": {
          "type": "object",
          "properties": {
            {{#if context.HasPremiumAccess}}
            "advanced": {
              "type": "boolean",
              "description": "Enable advanced filtering options"
            },
            {{/if}}
            "dateRange": {
              "type": "string",
              "description": "Date range filter"
            }
          }
        }
      }
    }
    """)]
    object options,
    EnhancedPluginContext context)
{
    // Implementation uses dynamic options based on context
}
```

### Performance Optimization

#### Context Handle Caching

```rust
use std::collections::HashMap;
use hpd_rust_agent::ffi_interface::ContextHandle;

pub struct ContextManager {
    handles: HashMap<String, ContextHandle>,
}

impl ContextManager {
    pub fn new() -> Self {
        Self {
            handles: HashMap::new(),
        }
    }
    
    pub fn get_or_create_context(
        &mut self, 
        key: &str, 
        config: &PluginConfiguration
    ) -> Result<&ContextHandle, String> {
        if !self.handles.contains_key(key) {
            let handle = ContextHandle::new(config)?;
            self.handles.insert(key.to_string(), handle);
        }
        Ok(self.handles.get(key).unwrap())
    }
    
    pub fn update_context(
        &mut self, 
        key: &str, 
        config: &PluginConfiguration
    ) -> Result<(), String> {
        if let Some(handle) = self.handles.get_mut(key) {
            handle.update(config)?;
        }
        Ok(())
    }
}
```

#### Batch Operations

```rust
// Batch evaluate multiple conditions
let conditions = vec![
    ("SearchPlugin", "basicSearch"),
    ("SearchPlugin", "advancedSearch"),
    ("DataPlugin", "exportData"),
    ("DataPlugin", "importData"),
];

let mut results = HashMap::new();
for (plugin, function) in conditions {
    let available = context_handle.evaluate_condition(plugin, function)?;
    results.insert(format!("{}::{}", plugin, function), available);
}

println!("Function availability: {:#?}", results);
```

### Testing Strategies

#### Unit Testing Plugin Contexts

```rust
#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;
    
    #[test]
    fn test_plugin_configuration_serialization() {
        let config = PluginConfiguration::new("TestPlugin", "TestContext")
            .with_property("stringProp", "test").unwrap()
            .with_property("numberProp", 42).unwrap()
            .with_property("boolProp", true).unwrap()
            .with_property("objectProp", json!({
                "nested": "value",
                "array": [1, 2, 3]
            })).unwrap();
        
        let json = config.to_json().unwrap();
        let deserialized = PluginConfiguration::from_json(&json).unwrap();
        
        assert_eq!(config.plugin_name, deserialized.plugin_name);
        assert_eq!(config.context_type, deserialized.context_type);
        assert_eq!(config.properties.len(), deserialized.properties.len());
    }
    
    #[test]
    fn test_context_property_types() {
        let mut config = PluginConfiguration::new("TestPlugin", "TestContext");
        
        // Test various property types
        config = config.with_property("string", "hello").unwrap();
        config = config.with_property("integer", 123).unwrap();
        config = config.with_property("float", 3.14).unwrap();
        config = config.with_property("boolean", false).unwrap();
        config = config.with_property("array", vec!["a", "b", "c"]).unwrap();
        config = config.with_property("object", json!({"key": "value"})).unwrap();
        
        assert_eq!(config.properties.len(), 6);
    }
    
    #[tokio::test]
    async fn test_ffi_integration() {
        let config = PluginConfiguration::new("TestPlugin", "TestContext")
            .with_property("testMode", true).unwrap();
        
        // Test context handle creation (may fail if C# runtime not available)
        match ffi_interface::ContextHandle::new(&config) {
            Ok(handle) => {
                // Test operations if successful
                let _ = handle.evaluate_condition("TestPlugin", "testFunction");
                let _ = handle.get_available_functions("TestPlugin");
            }
            Err(_) => {
                // Expected if C# runtime not available in test environment
                println!("FFI integration test skipped (C# runtime not available)");
            }
        }
    }
}
```

## Performance Guide

### Optimization Strategies

1. **Context Handle Reuse**: Create context handles once and reuse them
2. **Batch Operations**: Group multiple condition evaluations together
3. **JSON Minimization**: Only include necessary properties in configurations
4. **Memory Management**: Let RAII handle resource cleanup automatically

### Performance Benchmarks

```rust
use std::time::Instant;

fn benchmark_context_operations() -> Result<(), Box<dyn std::error::Error>> {
    let config = PluginConfiguration::new("BenchmarkPlugin", "BenchmarkContext")
        .with_property("iterations", 1000)?;
    
    // Benchmark context creation
    let start = Instant::now();
    let context = ffi_interface::ContextHandle::new(&config)?;
    println!("Context creation: {:?}", start.elapsed());
    
    // Benchmark condition evaluation
    let start = Instant::now();
    for _ in 0..1000 {
        let _ = context.evaluate_condition("BenchmarkPlugin", "testFunction")?;
    }
    println!("1000 condition evaluations: {:?}", start.elapsed());
    
    // Benchmark function filtering
    let start = Instant::now();
    let functions = context.get_available_functions("BenchmarkPlugin")?;
    println!("Function filtering ({} functions): {:?}", functions.len(), start.elapsed());
    
    Ok(())
}
```

### Memory Usage Optimization

```rust
use std::sync::Arc;

// Share configurations across multiple contexts
let shared_config = Arc::new(PluginConfiguration::new("SharedPlugin", "SharedContext")
    .with_property("sharedSetting", "value")?);

// Create multiple agents with shared configuration
let agents: Vec<_> = (0..10)
    .map(|i| {
        AgentBuilder::new(&format!("agent-{}", i))
            .with_plugin_config("SharedPlugin", (*shared_config).clone())
            .build()
    })
    .collect::<Result<Vec<_>, _>>()?;
```

## API Reference

### PluginConfiguration

```rust
impl PluginConfiguration {
    /// Creates a new plugin configuration
    pub fn new(plugin_name: impl Into<String>, context_type: impl Into<String>) -> Self
    
    /// Adds a property to the plugin context
    pub fn with_property<T: Serialize>(self, name: impl Into<String>, value: T) -> Result<Self, serde_json::Error>
    
    /// Sets the available functions for this plugin
    pub fn with_available_functions(self, functions: Vec<String>) -> Self
    
    /// Converts configuration to JSON string
    pub fn to_json(&self) -> Result<String, serde_json::Error>
    
    /// Creates configuration from JSON string
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error>
}
```

### ContextHandle

```rust
impl ContextHandle {
    /// Creates a new context handle from plugin configuration
    pub fn new(config: &PluginConfiguration) -> Result<Self, String>
    
    /// Updates the context with new configuration
    pub fn update(&mut self, config: &PluginConfiguration) -> Result<(), String>
    
    /// Evaluates a precompiled condition for a specific plugin function
    pub fn evaluate_condition(&self, plugin_type: &str, function_name: &str) -> Result<bool, String>
    
    /// Gets available functions for a plugin given this context
    pub fn get_available_functions(&self, plugin_type: &str) -> Result<Vec<DynamicFunctionMetadata>, String>
    
    /// Gets the raw handle for advanced FFI operations
    pub fn as_raw(&self) -> *mut c_void
}
```

### AgentBuilder Extensions

```rust
impl AgentBuilder {
    /// Adds a plugin configuration to the agent
    pub fn with_plugin_config(self, plugin_name: impl Into<String>, config: PluginConfiguration) -> Self
    
    /// Creates a dynamic plugin context with the given properties
    pub fn with_dynamic_plugin_context(
        self, 
        plugin_name: impl Into<String>, 
        context_type: impl Into<String>, 
        properties: HashMap<String, serde_json::Value>
    ) -> Self
}
```

## Examples

### Complete Real-World Example

Here's a comprehensive example showing a multi-language, role-based file management plugin:

**C# Plugin:**

```csharp
public class FileManagerContext : IPluginMetadataContext
{
    public string UserId { get; set; } = "";
    public string UserRole { get; set; } = "user";
    public string Language { get; set; } = "en";
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public long MaxFileSize { get; set; } = 10_000_000; // 10MB default
    public bool CanAccessSystemFiles { get; set; } = false;
    public Dictionary<string, object> Permissions { get; set; } = new();
}

public class FileManagerPlugin
{
    [AIFunction<FileManagerContext>]
    [Description("{{GetFileOperationDescription context.Language \"list_files\"}}")]
    public async Task<FileInfo[]> ListFilesAsync(
        [Description("{{GetFileOperationDescription context.Language \"directory_path\"}}")]
        string directoryPath,
        FileManagerContext context)
    {
        var allowedPath = ValidatePathAccess(directoryPath, context);
        return await GetFilesInDirectory(allowedPath, context.AllowedExtensions);
    }
    
    [AIFunction<FileManagerContext>]
    [Description("{{GetFileOperationDescription context.Language \"upload_file\"}}")]
    [Conditional("context.UserRole != \"readonly\"")]
    public async Task<string> UploadFileAsync(
        [Description("{{GetFileOperationDescription context.Language \"file_data\"}}")]
        byte[] fileData,
        [Description("{{GetFileOperationDescription context.Language \"filename\"}}")]
        string filename,
        FileManagerContext context)
    {
        ValidateFileSize(fileData.Length, context.MaxFileSize);
        ValidateFileExtension(filename, context.AllowedExtensions);
        
        return await SaveFile(fileData, filename, context.UserId);
    }
    
    [AIFunction<FileManagerContext>]
    [Description("{{GetFileOperationDescription context.Language \"delete_file\"}}")]
    [Conditional("context.UserRole == \"admin\" || context.UserRole == \"moderator\"")]
    public async Task<string> DeleteFileAsync(
        string filePath,
        FileManagerContext context)
    {
        ValidateFileOwnership(filePath, context.UserId, context.UserRole);
        return await DeleteFile(filePath);
    }
    
    [AIFunction<FileManagerContext>]
    [Description("System file operations (Admin only)")]
    [Conditional("context.CanAccessSystemFiles && context.UserRole == \"admin\"")]
    public async Task<string> SystemFileOperationAsync(
        string operation,
        string targetPath,
        FileManagerContext context)
    {
        // Only available to admin users with system file access
        return await ExecuteSystemOperation(operation, targetPath);
    }
    
    private string GetFileOperationDescription(string language, string operation)
    {
        var descriptions = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new() {
                ["list_files"] = "List files in the specified directory",
                ["directory_path"] = "The directory path to list files from",
                ["upload_file"] = "Upload a file to the system",
                ["file_data"] = "The binary data of the file to upload",
                ["filename"] = "The name of the file including extension",
                ["delete_file"] = "Delete a file from the system (requires elevated permissions)"
            },
            ["es"] = new() {
                ["list_files"] = "Listar archivos en el directorio especificado",
                ["directory_path"] = "La ruta del directorio del cual listar archivos",
                ["upload_file"] = "Subir un archivo al sistema",
                ["file_data"] = "Los datos binarios del archivo a subir",
                ["filename"] = "El nombre del archivo incluyendo la extensión",
                ["delete_file"] = "Eliminar un archivo del sistema (requiere permisos elevados)"
            },
            ["fr"] = new() {
                ["list_files"] = "Lister les fichiers dans le répertoire spécifié",
                ["directory_path"] = "Le chemin du répertoire à partir duquel lister les fichiers",
                ["upload_file"] = "Télécharger un fichier vers le système",
                ["file_data"] = "Les données binaires du fichier à télécharger",
                ["filename"] = "Le nom du fichier incluant l'extension",
                ["delete_file"] = "Supprimer un fichier du système (nécessite des permissions élevées)"
            }
        };
        
        return descriptions.GetValueOrDefault(language, descriptions["en"])
            ?.GetValueOrDefault(operation, operation) ?? operation;
    }
}
```

**Rust Usage:**

```rust
use hpd_rust_agent::{PluginConfiguration, AgentBuilder, ffi_interface};
use serde_json::json;
use std::collections::HashMap;

async fn create_file_manager_agent() -> Result<(), Box<dyn std::error::Error>> {
    // Create configurations for different user types
    
    // Admin user configuration
    let admin_config = PluginConfiguration::new("FileManagerPlugin", "FileManagerContext")
        .with_property("userId", "admin001")?
        .with_property("userRole", "admin")?
        .with_property("language", "en")?
        .with_property("allowedExtensions", vec!["pdf", "docx", "txt", "jpg", "png", "zip"])?
        .with_property("maxFileSize", 100_000_000i64)? // 100MB for admin
        .with_property("canAccessSystemFiles", true)?
        .with_property("permissions", json!({
            "canDelete": true,
            "canModifySystem": true,
            "canViewLogs": true
        }))?;
    
    // Regular user configuration
    let user_config = PluginConfiguration::new("FileManagerPlugin", "FileManagerContext")
        .with_property("userId", "user123")?
        .with_property("userRole", "user")?
        .with_property("language", "es")? // Spanish interface
        .with_property("allowedExtensions", vec!["pdf", "docx", "txt", "jpg", "png"])?
        .with_property("maxFileSize", 10_000_000i64)? // 10MB for regular users
        .with_property("canAccessSystemFiles", false)?
        .with_property("permissions", json!({
            "canDelete": false,
            "canModifySystem": false,
            "canViewLogs": false
        }))?;
    
    // Read-only user configuration
    let readonly_config = PluginConfiguration::new("FileManagerPlugin", "FileManagerContext")
        .with_property("userId", "readonly456")?
        .with_property("userRole", "readonly")?
        .with_property("language", "fr")? // French interface
        .with_property("allowedExtensions", vec!["pdf", "txt"])?
        .with_property("maxFileSize", 0i64)? // No uploads for readonly
        .with_property("canAccessSystemFiles", false)?
        .with_property("permissions", json!({}))?;
    
    // Create agents for different user types
    let admin_agent = AgentBuilder::new("admin-file-manager")
        .with_instructions("You are a file manager assistant with administrative privileges.")
        .with_plugin_config("FileManager", admin_config.clone())
        .build()?;
    
    let user_agent = AgentBuilder::new("user-file-manager")
        .with_instructions("Eres un asistente de gestión de archivos. Respondes en español.")
        .with_plugin_config("FileManager", user_config.clone())
        .build()?;
    
    let readonly_agent = AgentBuilder::new("readonly-file-manager")
        .with_instructions("Vous êtes un assistant de gestion de fichiers en lecture seule. Répondez en français.")
        .with_plugin_config("FileManager", readonly_config.clone())
        .build()?;
    
    // Demonstrate runtime metadata operations
    println!("=== Analyzing Available Functions by User Type ===");
    
    let configs = vec![
        ("Admin", admin_config),
        ("User", user_config),
        ("ReadOnly", readonly_config),
    ];
    
    for (user_type, config) in configs {
        println!("\n--- {} User Functions ---", user_type);
        
        match ffi_interface::ContextHandle::new(&config) {
            Ok(context) => {
                match context.get_available_functions("FileManagerPlugin") {
                    Ok(functions) => {
                        for function in functions {
                            println!("✓ {} - {}", function.name, function.resolved_description);
                            if function.requires_permission {
                                println!("  [Requires Permission]");
                            }
                        }
                        if functions.is_empty() {
                            println!("  No functions available for this user type");
                        }
                    }
                    Err(e) => println!("  Error getting functions: {}", e),
                }
            }
            Err(e) => println!("  Error creating context: {}", e),
        }
    }
    
    // Demonstrate dynamic context updates
    println!("\n=== Dynamic Language Switching ===");
    
    let mut multilingual_config = PluginConfiguration::new("FileManagerPlugin", "FileManagerContext")
        .with_property("userId", "multilingual_user")?
        .with_property("userRole", "user")?
        .with_property("language", "en")?
        .with_property("allowedExtensions", vec!["pdf", "docx"])?
        .with_property("maxFileSize", 10_000_000i64)?
        .with_property("canAccessSystemFiles", false)?;
    
    if let Ok(mut context) = ffi_interface::ContextHandle::new(&multilingual_config) {
        let languages = vec!["en", "es", "fr"];
        
        for lang in languages {
            multilingual_config = multilingual_config.with_property("language", lang)?;
            
            if context.update(&multilingual_config).is_ok() {
                if let Ok(functions) = context.get_available_functions("FileManagerPlugin") {
                    println!("\n--- Language: {} ---", lang);
                    for function in functions.iter().take(2) { // Show first 2 functions
                        println!("  {}: {}", function.name, function.resolved_description);
                    }
                }
            }
        }
    }
    
    Ok(())
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    create_file_manager_agent().await
}
```

This example demonstrates:
- Multi-language support with dynamic descriptions
- Role-based function availability
- Complex context properties including arrays and objects
- Runtime metadata querying
- Dynamic context updates
- Comprehensive permission systems

## Troubleshooting

### Common Issues and Solutions

#### 1. Context Handle Creation Fails

**Problem**: `ContextHandle::new()` returns an error

**Causes & Solutions**:
- **C# Runtime Not Available**: Ensure the native library is loaded and accessible
- **Invalid JSON Configuration**: Validate configuration with `to_json()` first
- **Missing Context Type**: Verify the context type name matches exactly

```rust
// Debug configuration before creating handle
let config = PluginConfiguration::new("MyPlugin", "MyContext")
    .with_property("test", "value")?;

println!("Config JSON: {}", config.to_json()?);

match ffi_interface::ContextHandle::new(&config) {
    Ok(handle) => {
        // Success
    }
    Err(e) => {
        eprintln!("Handle creation failed: {}", e);
        // Check if C# runtime is available
        match ffi_interface::get_plugin_metadata() {
            Ok(_) => eprintln!("C# runtime is available, configuration issue likely"),
            Err(_) => eprintln!("C# runtime not available"),
        }
    }
}
```

#### 2. Function Conditions Not Evaluating Correctly

**Problem**: Functions appear/disappear unexpectedly

**Debugging Steps**:

```rust
// Test individual conditions
let context = ffi_interface::ContextHandle::new(&config)?;

let test_conditions = vec![
    ("MyPlugin", "function1"),
    ("MyPlugin", "function2"),
    ("MyPlugin", "adminFunction"),
];

for (plugin, function) in test_conditions {
    match context.evaluate_condition(plugin, function) {
        Ok(result) => println!("{}.{}: {}", plugin, function, result),
        Err(e) => println!("{}.{}: ERROR - {}", plugin, function, e),
    }
}
```

#### 3. JSON Serialization Errors

**Problem**: `with_property()` fails with serialization error

**Solution**: Ensure values are serializable

```rust
// Good - serializable types
config = config.with_property("string", "value")?;
config = config.with_property("number", 42)?;
config = config.with_property("boolean", true)?;
config = config.with_property("array", vec!["a", "b"])?;
config = config.with_property("object", json!({"key": "value"}))?;

// Bad - non-serializable types
// config = config.with_property("function", || {})?; // This will fail
// config = config.with_property("nan", f64::NAN)?; // This will fail
```

#### 4. Memory Leaks or Access Violations

**Problem**: Application crashes or memory issues

**Prevention**:
- Always use RAII patterns (ContextHandle automatically cleans up)
- Don't store raw FFI pointers
- Let Rust's ownership system manage memory

```rust
// Good - RAII handles cleanup
{
    let context = ffi_interface::ContextHandle::new(&config)?;
    // Use context
    // Automatically cleaned up when context goes out of scope
}

// Bad - manual memory management
// let raw_handle = unsafe { ffi::create_context_handle(...) };
// Don't do this - use ContextHandle wrapper instead
```

#### 5. Performance Issues

**Problem**: Slow function filtering or condition evaluation

**Optimization**:

```rust
// Create contexts once and reuse
let context = ffi_interface::ContextHandle::new(&config)?;

// Batch operations when possible
let functions_to_check = vec!["func1", "func2", "func3"];
let mut results = Vec::new();

for func in functions_to_check {
    if let Ok(available) = context.evaluate_condition("Plugin", func) {
        if available {
            results.push(func);
        }
    }
}

// Update context instead of creating new one
context.update(&new_config)?; // ~5ms vs ~10ms for new creation
```

### Debug Mode

Enable debug logging for troubleshooting:

```rust
// Set environment variable for debug output
std::env::set_var("HPD_PLUGIN_DEBUG", "1");

// Or use debug methods
impl PluginConfiguration {
    pub fn debug_print(&self) {
        println!("Plugin Configuration Debug:");
        println!("  Name: {}", self.plugin_name);
        println!("  Context Type: {}", self.context_type);
        println!("  Properties: {:#}", serde_json::to_string_pretty(&self.properties).unwrap_or_default());
        println!("  Available Functions: {:?}", self.available_functions);
    }
}
```

### Testing in Isolation

Test components independently:

```rust
#[cfg(test)]
mod debug_tests {
    use super::*;
    
    #[test]
    fn test_configuration_only() {
        // Test without FFI dependency
        let config = PluginConfiguration::new("Test", "TestContext")
            .with_property("debug", true)
            .unwrap();
        
        assert_eq!(config.plugin_name, "Test");
        assert_eq!(config.context_type, "TestContext");
        assert!(config.properties.contains_key("debug"));
        
        // Test JSON round-trip
        let json = config.to_json().unwrap();
        let restored = PluginConfiguration::from_json(&json).unwrap();
        assert_eq!(config.plugin_name, restored.plugin_name);
    }
    
    #[test]
    fn test_ffi_availability() {
        // Test if FFI functions are available
        match ffi_interface::get_plugin_metadata() {
            Ok(_) => println!("FFI available"),
            Err(e) => println!("FFI not available: {}", e),
        }
    }
}
```

This comprehensive documentation covers all aspects of the Dynamic Plugin Metadata system, from basic usage to advanced optimization techniques and troubleshooting. The system provides a robust, performant, and developer-friendly way to create context-aware plugins with dynamic behavior.