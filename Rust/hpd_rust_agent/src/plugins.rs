use std::collections::HashMap;
use std::sync::Mutex;
use serde_json::Value as JsonValue;
use once_cell::sync::Lazy;
use std::pin::Pin;
use std::future::Future;

type AsyncFunctionExecutor = Box<dyn Fn(String) -> Pin<Box<dyn Future<Output = Result<String, String>> + Send>> + Send + Sync>;

static FUNCTION_EXECUTORS: Lazy<Mutex<HashMap<String, AsyncFunctionExecutor>>> = 
    Lazy::new(|| Mutex::new(HashMap::new()));

/// Register an async function executor
pub fn register_async_executor(name: String, executor: AsyncFunctionExecutor) {
    if let Ok(mut registry) = FUNCTION_EXECUTORS.lock() {
        println!("Registered async executor for function: {}", name);
        registry.insert(name, executor);
    }
}

/// Execute a registered function asynchronously
pub async fn execute_function_async(name: &str, args_json: &str) -> Result<String, String> {
    let executor = {
        let registry = FUNCTION_EXECUTORS.lock()
            .map_err(|_| "Failed to lock function executor registry".to_string())?;
        
        // We can't clone the executor, so we need a different approach
        if registry.contains_key(name) {
            // The executor exists, we'll call it while holding the lock
            drop(registry);
            true
        } else {
            false
        }
    };
    
    if executor {
        // Re-acquire the lock to call the function
        let registry = FUNCTION_EXECUTORS.lock()
            .map_err(|_| "Failed to lock function executor registry".to_string())?;
        
        if let Some(exec) = registry.get(name) {
            exec(args_json.to_string()).await
        } else {
            Err(format!("Function '{}' not found in executor registry", name))
        }
    } else {
        Err(format!("Function '{}' not found in executor registry", name))
    }
}

/// Plugin registration information
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct PluginRegistration {
    pub name: String,
    pub description: String,
    pub functions: Vec<(String, String)>, // (function_name, wrapper_function_name)
    pub schemas: HashMap<String, String>,
}

/// Global plugin registry
static PLUGIN_REGISTRY: Mutex<Vec<PluginRegistration>> = Mutex::new(Vec::new());

/// Register a plugin with the global registry
pub fn register_plugin(plugin: PluginRegistration) {
    if let Ok(mut registry) = PLUGIN_REGISTRY.lock() {
        registry.push(plugin);
        println!("Registered plugin: {}", registry.last().unwrap().name);
    }
}

/// Get all registered plugins
pub fn get_registered_plugins() -> Vec<PluginRegistration> {
    PLUGIN_REGISTRY.lock().unwrap_or_else(|_| {
        println!("Warning: Plugin registry lock was poisoned");
        std::process::exit(1);
    }).clone()
}

/// Get a specific plugin by name
pub fn get_plugin(name: &str) -> Option<PluginRegistration> {
    PLUGIN_REGISTRY.lock().ok()?.iter().find(|p| p.name == name).cloned()
}

/// Get all function schemas as a single JSON object
pub fn get_all_schemas() -> JsonValue {
    let plugins = get_registered_plugins();
    let mut all_schemas = serde_json::Map::new();
    
    for plugin in plugins {
        for (func_name, schema_str) in plugin.schemas {
            if let Ok(schema) = serde_json::from_str::<JsonValue>(&schema_str) {
                all_schemas.insert(func_name.to_string(), schema);
            }
        }
    }
    
    JsonValue::Object(all_schemas)
}

/// List all available function names
pub fn list_functions() -> Vec<String> {
    let plugins = get_registered_plugins();
    let mut functions = Vec::new();
    
    for plugin in plugins {
        for (func_name, _) in plugin.functions {
            functions.push(func_name.to_string());
        }
    }
    
    functions
}

/// Get plugin statistics
pub fn get_plugin_stats() -> JsonValue {
    let plugins = get_registered_plugins();
    let total_plugins = plugins.len();
    let total_functions: usize = plugins.iter().map(|p| p.functions.len()).sum();
    
    serde_json::json!({
        "total_plugins": total_plugins,
        "total_functions": total_functions,
        "plugins": plugins.iter().map(|p| serde_json::json!({
            "name": p.name,
            "description": p.description,
            "function_count": p.functions.len()
        })).collect::<Vec<_>>()
    })
}
