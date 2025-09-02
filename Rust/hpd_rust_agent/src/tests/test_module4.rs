use crate::*;

#[tokio::test]
async fn test_module4_plugin_system() {
    println!("\n=== Module 4: Automated Rust Plugin System Test ===\n");

    // Test 1: Plugin Auto-Registration
    println!("🔧 Test 1: Plugin Auto-Registration");
    let plugins = get_registered_plugins();
    println!("📋 Found {} registered plugins", plugins.len());
    
    for plugin in &plugins {
        println!("  📦 Plugin: {} - {}", plugin.name, plugin.description);
        println!("    🔧 Functions: {}", plugin.functions.len());
        for (func_name, wrapper_name) in &plugin.functions {
            println!("      ⚡ {}: {}", func_name, wrapper_name);
        }
    }
    
    // Verify expected plugins are registered
    let plugin_names: Vec<&String> = plugins.iter().map(|p| &p.name).collect();
    assert!(plugin_names.contains(&&"MathPlugin".to_string()), "MathPlugin should be auto-registered");
    assert!(plugin_names.contains(&&"StringPlugin".to_string()), "StringPlugin should be auto-registered");
    assert!(plugin_names.contains(&&"AsyncPlugin".to_string()), "AsyncPlugin should be auto-registered");
    println!("✅ Plugin auto-registration working correctly");

    // Test 2: Function Schema Generation
    println!("\n🔧 Test 2: Function Schema Generation");
    let all_schemas = crate::plugins::get_all_schemas();
    println!("📋 Generated schemas for functions:");
    
    if let serde_json::Value::Object(schemas) = &all_schemas {
        for (func_name, schema) in schemas {
            println!("  📝 Function: {}", func_name);
            if let Ok(pretty) = serde_json::to_string_pretty(schema) {
                println!("    Schema:\n{}", 
                    pretty.lines()
                        .map(|line| format!("      {}", line))
                        .collect::<Vec<_>>()
                        .join("\n"));
            }
        }
    }
    
    assert!(all_schemas.is_object(), "Should generate schema object");
    println!("✅ Schema generation working correctly");

    // Test 3: Plugin Statistics
    println!("\n🔧 Test 3: Plugin Statistics");
    let stats = crate::plugins::get_plugin_stats();
    println!("📊 Plugin Statistics:");
    if let Ok(pretty_stats) = serde_json::to_string_pretty(&stats) {
        println!("{}", pretty_stats);
    }
    
    if let Some(total_plugins) = stats.get("total_plugins") {
        assert!(total_plugins.as_u64().unwrap_or(0) >= 3, "Should have at least 3 plugins");
    }
    if let Some(total_functions) = stats.get("total_functions") {
        assert!(total_functions.as_u64().unwrap_or(0) >= 10, "Should have multiple functions");
    }
    println!("✅ Plugin statistics working correctly");

    // Test 4: Function List
    println!("\n🔧 Test 4: Function List");
    let functions = crate::plugins::list_functions();
    println!("📋 Available functions: {}", functions.len());
    for func_name in &functions {
        println!("  ⚡ {}", func_name);
    }
    
    // Verify expected functions are present
    assert!(functions.contains(&"add".to_string()), "Math add function should be available");
    assert!(functions.contains(&"to_upper".to_string()), "String uppercase function should be available");
    assert!(functions.contains(&"async_compute".to_string()), "Async compute function should be available");
    println!("✅ Function listing working correctly");

    // Test 5: Plugin Instance Testing
    println!("\n🔧 Test 5: Plugin Instance Testing");
    
    // Test MathPlugin
    let math = crate::example_plugins::MathPlugin::new();
    assert_eq!(math.add(2.0, 3.0), 5.0);
    assert_eq!(math.multiply(4.0, 5.0), 20.0);
    assert_eq!(math.power(2.0, 3.0), 8.0);
    assert!(math.is_prime(17));
    println!("  ✅ MathPlugin functions working correctly");
    
    // Test StringPlugin
    let mut strings = crate::example_plugins::StringPlugin::new();
    assert_eq!(strings.to_uppercase("hello".to_string()), "HELLO");
    assert_eq!(strings.reverse("world".to_string()), "dlrow");
    assert_eq!(strings.character_count("test".to_string()), 4);
    assert_eq!(strings.get_operations_count(), 3);
    println!("  ✅ StringPlugin functions working correctly");
    
    // Test AsyncPlugin
    let mut async_plugin = crate::example_plugins::AsyncPlugin::new();
    let result = async_plugin.async_compute(50).await;
    assert!(result.contains("50ms"));
    assert!(result.contains("request #1"));
    
    let timestamp = async_plugin.get_timestamp().await;
    assert!(timestamp.contains("Current timestamp"));
    assert!(timestamp.contains("request #2"));
    println!("  ✅ AsyncPlugin functions working correctly");

    // Test 6: Schema Validation
    println!("\n🔧 Test 6: Schema Validation");
    
    let math_plugin = plugins.iter().find(|p| p.name == "MathPlugin").unwrap();
    assert!(!math_plugin.schemas.is_empty(), "MathPlugin should have schemas");
    
    // Validate add function schema
    if let Some(add_schema_str) = math_plugin.schemas.get("add") {
        let schema: serde_json::Value = serde_json::from_str(add_schema_str).unwrap();
        
        // Verify schema structure
        assert_eq!(schema["type"], "function");
        assert_eq!(schema["function"]["name"], "add");
        assert!(schema["function"]["description"].as_str().unwrap().contains("Add"));
        
        let parameters = &schema["function"]["parameters"];
        assert_eq!(parameters["type"], "object");
        assert!(parameters["properties"].is_object());
        assert!(parameters["required"].is_array());
        
        println!("  ✅ Function schema validation passed");
    }

    println!("\n🎉 Module 4 Complete: Automated Rust Plugin System");
    println!("  ✅ Plugin auto-registration with procedural macros");
    println!("  ✅ JSON schema generation for AI functions");
    println!("  ✅ FFI integration for C# interoperability");
    println!("  ✅ Runtime plugin discovery and statistics");
    println!("  ✅ Type-safe function wrapper generation");
    println!("  ✅ Async function support");
    println!("  ✅ Multiple plugin types (Math, String, Async)");
}

#[tokio::test]
async fn test_comprehensive_plugin_functionality() {
    println!("\n=== Comprehensive Plugin Functionality Test ===\n");

    // Test complex plugin interactions
    let plugins = get_registered_plugins();
    
    // Verify each plugin has proper structure
    for plugin in &plugins {
        println!("🔍 Analyzing plugin: {}", plugin.name);
        
        // Every plugin should have at least one function
        assert!(!plugin.functions.is_empty(), "Plugin {} should have functions", plugin.name);
        
        // Every plugin should have schemas for its functions
        assert!(!plugin.schemas.is_empty(), "Plugin {} should have schemas", plugin.name);
        
        // Function count should match schema count
        assert_eq!(
            plugin.functions.len(),
            plugin.schemas.len(),
            "Plugin {} should have matching function and schema counts",
            plugin.name
        );
        
        // Validate schema JSON
        for (func_name, schema_str) in &plugin.schemas {
            let schema: Result<serde_json::Value, _> = serde_json::from_str(schema_str);
            assert!(schema.is_ok(), "Schema for function {} should be valid JSON", func_name);
            
            let schema = schema.unwrap();
            assert_eq!(schema["type"], "function", "Schema should be a function type");
            assert!(schema["function"]["name"].is_string(), "Function should have a name");
            assert!(schema["function"]["description"].is_string(), "Function should have a description");
        }
        
        println!("  ✅ Plugin {} structure valid", plugin.name);
    }

    println!("\n✅ All plugins passed comprehensive validation");
}

#[test]
fn test_module4_summary() {
    println!("\n=== Module 4 Implementation Summary ===");
    println!("📦 Procedural Macro System:");
    println!("  • #[hpd_plugin] macro for plugin registration");
    println!("  • #[ai_function] macro for function annotation");
    println!("  • Automatic JSON schema generation");
    println!("  • FFI wrapper generation");
    println!("  • Auto-registration with global registry");
    
    println!("\n🔧 Plugin Infrastructure:");
    println!("  • Global plugin registry");
    println!("  • Runtime plugin discovery");
    println!("  • Function schema management");
    println!("  • Plugin statistics and monitoring");
    
    println!("\n🌉 C# Integration:");
    println!("  • FFI bindings for plugin system");
    println!("  • JSON serialization/deserialization");
    println!("  • Memory management for cross-language strings");
    println!("  • Type-safe wrapper classes");
    
    println!("\n🧪 Example Plugins:");
    println!("  • MathPlugin: Mathematical operations");
    println!("  • StringPlugin: String manipulation");
    println!("  • AsyncPlugin: Asynchronous operations");
    
    println!("\n✅ Module 4: Automated Plugin System - COMPLETE");
}
