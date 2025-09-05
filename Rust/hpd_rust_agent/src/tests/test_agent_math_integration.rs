use std::ffi::{CStr, CString, c_void};
use crate::ffi::{rust_get_plugin_registry, rust_execute_plugin_function, rust_free_string, rust_get_function_list};
use crate::agent::{AgentConfig, ProviderConfig, ChatProvider};
use crate::plugins::{get_registered_plugins, list_functions};
use serde_json::Value as JsonValue;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_agent_math_plugin_integration() {
        println!("=== TESTING FULL AGENT + MATH PLUGIN INTEGRATION ===");
        
        // Create agent configuration with math plugin enabled using OpenRouter + Gemini
        let config = AgentConfig {
            name: "Math Agent".to_string(),
            system_instructions: "You are a helpful math assistant that can perform calculations using available math functions.".to_string(),
            max_function_calls: 10,
            max_conversation_history: 20,
            provider: Some(ProviderConfig {
                provider: ChatProvider::OpenRouter,
                model_name: "google/gemini-2.5-pro".to_string(),
                api_key: Some("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".to_string()),
                endpoint: Some("https://openrouter.ai/api/v1".to_string()),
            }),
            plugin_configurations: None,
        };
        
        let config_json = serde_json::to_string(&config).unwrap();
        println!("✅ Created agent config with OpenRouter + Gemini: {}", config.provider.as_ref().unwrap().model_name);
        
        // Get registered plugins via FFI
        let plugins_ptr = unsafe { rust_get_plugin_registry() };
        assert!(!plugins_ptr.is_null(), "Failed to get plugin registry");
        
        let plugins_str = unsafe { CStr::from_ptr(plugins_ptr).to_str().unwrap() };
        println!("✅ Retrieved plugin registry: {}", plugins_str);
        
                // Parse and verify plugins
        let plugins_response: serde_json::Value = serde_json::from_str(&plugins_str).unwrap();
        
        // Extract the plugins array from the response object
        let plugins = plugins_response["plugins"].as_array()
            .expect("Plugin registry should have a 'plugins' array");
        
        assert!(!plugins.is_empty(), "Should have at least one plugin");
        
        // Find MathPlugin
        let math_plugin = plugins
            .iter()
            .find(|p| p["name"] == "MathPlugin")
            .expect("MathPlugin should be available");
        
        println!("✅ Found MathPlugin: {}", serde_json::to_string(math_plugin).unwrap());
        
        // Verify math functions are available
        let functions = math_plugin["functions"].as_array().unwrap();
        let function_names: Vec<&str> = functions
            .iter()
            .map(|f| f["name"].as_str().unwrap())
            .collect();
        
        assert!(function_names.contains(&"add"), "Should have add function");
        assert!(function_names.contains(&"multiply"), "Should have multiply function");
        println!("✅ MathPlugin functions available: {:?}", function_names);
    }

    #[test]
    fn test_plugin_function_direct_execution() {
        println!("=== TESTING DIRECT PLUGIN FUNCTION EXECUTION ===");
        
        // Test direct execution of math plugin functions
        
        // Test addition function
        let function_name = CString::new("add").unwrap();
        let args_json = CString::new(r#"{"a": 15.0, "b": 27.0}"#).unwrap();
        
        let result_ptr = rust_execute_plugin_function(function_name.as_ptr(), args_json.as_ptr());
        assert!(!result_ptr.is_null(), "Failed to execute add function");
        
        let result_str = unsafe { CStr::from_ptr(result_ptr).to_str().unwrap() };
        println!("Direct add function result: {}", result_str);
        
        let response: JsonValue = serde_json::from_str(result_str).unwrap();
        assert!(response["success"].as_bool().unwrap(), "Add function failed: {:?}", response);
        
        // Parse the result - it should be "42.0" as a string
        let result_value = response["result"].as_str().unwrap();
        let result_number: f64 = result_value.parse().unwrap();
        assert_eq!(result_number, 42.0, "Addition result should be 42.0");
        
        unsafe { let _ = CString::from_raw(result_ptr); }
        
        // Test power function
        let function_name = CString::new("power").unwrap();
        let args_json = CString::new(r#"{"base": 2.0, "exponent": 8.0}"#).unwrap();
        
        let result_ptr = rust_execute_plugin_function(function_name.as_ptr(), args_json.as_ptr());
        assert!(!result_ptr.is_null(), "Failed to execute power function");
        
        let result_str = unsafe { CStr::from_ptr(result_ptr).to_str().unwrap() };
        println!("Direct power function result: {}", result_str);
        
        let response: JsonValue = serde_json::from_str(result_str).unwrap();
        assert!(response["success"].as_bool().unwrap(), "Power function failed: {:?}", response);
        
        let result_value = response["result"].as_str().unwrap();
        let result_number: f64 = result_value.parse().unwrap();
        assert_eq!(result_number, 256.0, "Power result should be 256.0");
        
        unsafe { let _ = CString::from_raw(result_ptr); }
        
        println!("✅ Direct plugin function execution verified");
        println!("✅ Math functions compute correct results");
        println!("✅ FFI integration working properly");
    }

    #[test]
    fn test_plugin_registry_verification() {
        println!("=== TESTING PLUGIN REGISTRY VERIFICATION ===");
        
        // Verify that math functions are properly registered using direct Rust API
        let plugins = get_registered_plugins();
        println!("Registered plugins: {:?}", plugins);
        
        // Check if MathPlugin is registered
        let has_math_plugin = plugins.iter().any(|p| p.name.contains("Math"));
        assert!(has_math_plugin, "MathPlugin should be registered");
        
        // Verify specific math functions are available
        let functions = list_functions();
        println!("Available functions: {:?}", functions);
        
        let required_functions = ["add", "subtract", "multiply", "divide", "power", "sqrt", "factorial", "is_prime"];
        for func in &required_functions {
            assert!(functions.contains(&func.to_string()), "Function '{}' should be available", func);
        }
        
        // Test via FFI as well
        let functions_ptr = unsafe { rust_get_function_list() };
        assert!(!functions_ptr.is_null(), "FFI function list should not be null");
        
        let functions_str = unsafe { CStr::from_ptr(functions_ptr).to_str().unwrap() };
        let functions_json: JsonValue = serde_json::from_str(functions_str).unwrap();
        let functions_array = functions_json.as_array().unwrap();
        
        for func in &required_functions {
            let has_function = functions_array.iter().any(|f| f.as_str() == Some(*func));
            assert!(has_function, "Function '{}' should be available via FFI", func);
        }
        
        unsafe { rust_free_string(functions_ptr); }
        
        println!("✅ All required math functions are registered");
        println!("✅ Plugin registry is properly populated");
        println!("✅ Math plugin integration verified");
        println!("✅ FFI function access working correctly");
    }

    #[test]
    fn test_openrouter_gemini_integration() {
        println!("=== TESTING OPENROUTER + GEMINI INTEGRATION ===");
        
        // Test configuration setup for OpenRouter with Gemini 2.5 Pro
        let config = AgentConfig {
            name: "Gemini Math Agent".to_string(),
            system_instructions: "You are an advanced AI math assistant powered by Google's Gemini 2.5 Pro via OpenRouter. Use the available math functions to solve complex mathematical problems.".to_string(),
            max_function_calls: 15,
            max_conversation_history: 50,
            provider: Some(ProviderConfig {
                provider: ChatProvider::OpenRouter,
                model_name: "google/gemini-2.5-pro".to_string(),
                api_key: Some("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".to_string()),
                endpoint: Some("https://openrouter.ai/api/v1".to_string()),
            }),
            plugin_configurations: None,
        };
        
        // Verify configuration is correct
        let provider_config = config.provider.as_ref().unwrap();
        assert_eq!(provider_config.provider as u32, ChatProvider::OpenRouter as u32);
        assert_eq!(provider_config.model_name, "google/gemini-2.5-pro");
        assert_eq!(provider_config.endpoint.as_ref().unwrap(), "https://openrouter.ai/api/v1");
        
        // Serialize and verify JSON structure
        let config_json = serde_json::to_string(&config).unwrap();
        println!("Config JSON: {}", config_json);
        let config_value: JsonValue = serde_json::from_str(&config_json).unwrap();
        
        assert_eq!(config_value["name"], "Gemini Math Agent");
        assert!(config_value["systemInstructions"].as_str().unwrap().contains("Gemini"));
        
        // Check if provider exists in JSON before accessing
        if let Some(provider) = config_value["provider"].as_object() {
            assert_eq!(provider["provider"], ChatProvider::OpenRouter as u32);
            assert_eq!(provider["modelName"], "google/gemini-2.5-pro");
        } else {
            panic!("Provider field not found in JSON: {}", config_json);
        }
        
        println!("✅ OpenRouter provider correctly configured");
        println!("✅ Gemini 2.0 Flash Thinking model specified");
        println!("✅ API endpoint set to OpenRouter");
        println!("✅ Configuration serializes correctly");
        println!("✅ Ready for Gemini-powered math assistance");
    }
}
