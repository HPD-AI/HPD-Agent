use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// Configuration for context-aware plugin behavior.
/// Enables runtime plugin function filtering based on dynamic context properties.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PluginConfiguration {
    /// The name of the plugin type (e.g., "WebSearchPlugin").
    /// Should match the plugin class name.
    #[serde(rename = "pluginName")]
    pub plugin_name: String,

    /// The context type name for validation (e.g., "WebSearchPluginMetadataContext").
    /// Should match the TContext generic parameter in AIFunction<TContext>.
    #[serde(rename = "contextType")]
    pub context_type: String,

    /// Dynamic properties to be injected into the plugin context at runtime.
    /// Keys should match property names on the context type.
    #[serde(rename = "properties")]
    pub properties: HashMap<String, serde_json::Value>,

    /// Optional list of specific functions to make available.
    /// If None or empty, all functions (subject to conditional filtering) will be available.
    #[serde(rename = "availableFunctions")]
    pub available_functions: Option<Vec<String>>,
}

impl PluginConfiguration {
    /// Creates a new plugin configuration.
    pub fn new(plugin_name: impl Into<String>, context_type: impl Into<String>) -> Self {
        Self {
            plugin_name: plugin_name.into(),
            context_type: context_type.into(),
            properties: HashMap::new(),
            available_functions: None,
        }
    }

    /// Adds a property to the plugin context.
    pub fn with_property<T: Serialize>(mut self, name: impl Into<String>, value: T) -> Result<Self, serde_json::Error> {
        let json_value = serde_json::to_value(value)?;
        self.properties.insert(name.into(), json_value);
        Ok(self)
    }

    /// Sets the available functions for this plugin.
    pub fn with_available_functions(mut self, functions: Vec<String>) -> Self {
        self.available_functions = Some(functions);
        self
    }

    /// Converts this configuration to JSON string.
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(self)
    }

    /// Creates a configuration from JSON string.
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(json)
    }
}

/// Metadata about a plugin function that has been dynamically resolved.
/// Returned by FFI functions for consumption.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DynamicFunctionMetadata {
    /// The name of the function.
    pub name: String,
    
    /// The resolved description after template processing.
    #[serde(rename = "resolvedDescription")]
    pub resolved_description: String,
    
    /// The JSON schema for the function parameters.
    pub schema: HashMap<String, serde_json::Value>,
    
    /// Whether this function is available given the current context.
    #[serde(rename = "isAvailable")]
    pub is_available: bool,
    
    /// Whether this function requires special permissions.
    #[serde(rename = "requiresPermission")]
    pub requires_permission: bool,
}

/// Plugin context for managing runtime properties and state.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PluginContext {
    /// The properties available to the plugin context.
    pub properties: HashMap<String, serde_json::Value>,
}

impl PluginContext {
    /// Creates a new empty plugin context.
    pub fn new() -> Self {
        Self {
            properties: HashMap::new(),
        }
    }

    /// Creates a plugin context from a configuration.
    pub fn from_configuration(config: &PluginConfiguration) -> Self {
        Self {
            properties: config.properties.clone(),
        }
    }

    /// Sets a property in the context.
    pub fn set_property<T: Serialize>(&mut self, name: &str, value: T) -> Result<(), serde_json::Error> {
        let json_value = serde_json::to_value(value)?;
        self.properties.insert(name.to_string(), json_value);
        Ok(())
    }

    /// Gets a property from the context with type conversion.
    pub fn get_property<T: serde::de::DeserializeOwned>(&self, name: &str) -> Option<T> {
        self.properties.get(name)
            .and_then(|value| serde_json::from_value(value.clone()).ok())
    }

    /// Checks if a property exists.
    pub fn has_property(&self, name: &str) -> bool {
        self.properties.contains_key(name)
    }

    /// Gets all property names.
    pub fn property_names(&self) -> Vec<&String> {
        self.properties.keys().collect()
    }

    /// Converts the context to JSON.
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(&self.properties)
    }

    /// Creates a context from JSON.
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error> {
        let properties = serde_json::from_str(json)?;
        Ok(Self { properties })
    }
}

impl Default for PluginContext {
    fn default() -> Self {
        Self::new()
    }
}

/// High-level Rust interface for Phase 2 FFI functions.
/// Provides safe wrappers around the raw FFI calls with proper memory management.
pub mod ffi_interface {
    use std::ffi::{CStr, CString, c_void};
    use std::ptr;
    use super::*;
    use crate::ffi;

    /// Handle to a context managed by the C# side via FFI.
    /// Automatically destroys the context when dropped.
    pub struct ContextHandle {
        handle: *mut c_void,
    }

    impl ContextHandle {
        /// Creates a new context handle from a plugin configuration.
        pub fn new(config: &PluginConfiguration) -> Result<Self, String> {
            let json = config.to_json()
                .map_err(|e| format!("Failed to serialize config: {}", e))?;
            let c_json = CString::new(json)
                .map_err(|e| format!("Failed to create CString: {}", e))?;
            
            let handle = unsafe { ffi::create_context_handle(c_json.as_ptr()) };
            if handle.is_null() {
                Err("Failed to create context handle".to_string())
            } else {
                Ok(ContextHandle { handle })
            }
        }

        /// Updates the context with a new configuration.
        pub fn update(&mut self, config: &PluginConfiguration) -> Result<(), String> {
            let json = config.to_json()
                .map_err(|e| format!("Failed to serialize config: {}", e))?;
            let c_json = CString::new(json)
                .map_err(|e| format!("Failed to create CString: {}", e))?;
            
            let success = unsafe { ffi::update_context_handle(self.handle, c_json.as_ptr()) };
            if success {
                Ok(())
            } else {
                Err("Failed to update context handle".to_string())
            }
        }

        /// Evaluates a precompiled condition for a specific plugin function.
        pub fn evaluate_condition(&self, plugin_type: &str, function_name: &str) -> Result<bool, String> {
            let c_plugin_type = CString::new(plugin_type)
                .map_err(|e| format!("Failed to create CString for plugin type: {}", e))?;
            let c_function_name = CString::new(function_name)
                .map_err(|e| format!("Failed to create CString for function name: {}", e))?;

            let result = unsafe { 
                ffi::evaluate_precompiled_condition(
                    c_plugin_type.as_ptr(), 
                    c_function_name.as_ptr(), 
                    self.handle
                )
            };
            Ok(result)
        }

        /// Gets available functions for a plugin given this context.
        pub fn get_available_functions(&self, plugin_type: &str) -> Result<Vec<DynamicFunctionMetadata>, String> {
            let c_plugin_type = CString::new(plugin_type)
                .map_err(|e| format!("Failed to create CString for plugin type: {}", e))?;

            let result_ptr = unsafe { ffi::filter_available_functions(c_plugin_type.as_ptr(), self.handle) };
            if result_ptr.is_null() {
                return Err("FFI function returned null".to_string());
            }

            // Convert the returned JSON to Rust types
            let json_str = unsafe {
                let c_str = CStr::from_ptr(result_ptr);
                c_str.to_str().map_err(|e| format!("Invalid UTF-8 from C#: {}", e))?
            };

            let metadata: Vec<DynamicFunctionMetadata> = serde_json::from_str(json_str)
                .map_err(|e| format!("Failed to parse JSON: {}", e))?;

            // Free the string allocated by C#
            unsafe { ffi::free_string(result_ptr as *mut c_void) };

            Ok(metadata)
        }

        /// Gets the raw handle for advanced FFI operations.
        pub fn as_raw(&self) -> *mut c_void {
            self.handle
        }
    }

    impl Drop for ContextHandle {
        fn drop(&mut self) {
            if !self.handle.is_null() {
                unsafe { ffi::destroy_context_handle(self.handle) };
                self.handle = ptr::null_mut();
            }
        }
    }

    /// Gets metadata for all registered plugins from C#.
    pub fn get_plugin_metadata() -> Result<serde_json::Value, String> {
        let result_ptr = unsafe { ffi::get_plugin_metadata_json() };
        if result_ptr.is_null() {
            return Err("FFI function returned null".to_string());
        }

        let json_str = unsafe {
            let c_str = CStr::from_ptr(result_ptr);
            c_str.to_str().map_err(|e| format!("Invalid UTF-8 from C#: {}", e))?
        };

        let metadata: serde_json::Value = serde_json::from_str(json_str)
            .map_err(|e| format!("Failed to parse JSON: {}", e))?;

        // Free the string allocated by C#
        unsafe { ffi::free_string(result_ptr as *mut c_void) };

        Ok(metadata)
    }

    // Thread-safe implementation
    unsafe impl Send for ContextHandle {}
    unsafe impl Sync for ContextHandle {}
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_plugin_configuration_creation() {
        let config = PluginConfiguration::new("WebSearchPlugin", "WebSearchPluginMetadataContext")
            .with_property("provider", "Tavily").unwrap()
            .with_property("maxResults", 10).unwrap()
            .with_available_functions(vec!["search".to_string(), "search_images".to_string()]);

        assert_eq!(config.plugin_name, "WebSearchPlugin");
        assert_eq!(config.context_type, "WebSearchPluginMetadataContext");
        assert!(config.properties.contains_key("provider"));
        assert!(config.properties.contains_key("maxResults"));
        assert_eq!(config.available_functions.as_ref().unwrap().len(), 2);
    }

    #[test]
    fn test_plugin_context_operations() {
        let mut context = PluginContext::new();
        
        context.set_property("testString", "value").unwrap();
        context.set_property("testNumber", 42).unwrap();
        context.set_property("testBool", true).unwrap();

        assert_eq!(context.get_property::<String>("testString"), Some("value".to_string()));
        assert_eq!(context.get_property::<i32>("testNumber"), Some(42));
        assert_eq!(context.get_property::<bool>("testBool"), Some(true));
        assert!(context.has_property("testString"));
        assert!(!context.has_property("nonexistent"));
    }

    #[test]
    fn test_json_serialization() {
        let config = PluginConfiguration::new("TestPlugin", "TestContext")
            .with_property("key", "value").unwrap();

        let json = config.to_json().unwrap();
        let deserialized = PluginConfiguration::from_json(&json).unwrap();

        assert_eq!(config.plugin_name, deserialized.plugin_name);
        assert_eq!(config.context_type, deserialized.context_type);
        assert_eq!(config.properties, deserialized.properties);
    }
}