use serde::Serialize;
use std::ffi::{CString, c_void};
use std::collections::HashMap;
use crate::ffi;
use crate::plugins::PluginRegistration;
use crate::plugin_context::PluginConfiguration;

/// Trait that all plugins must implement
/// This is implemented automatically by the #[hpd_plugin] macro
pub trait Plugin {
    /// Register all functions from this plugin with the global registry
    fn register_functions(&self);
    
    /// Get metadata about this plugin for C# consumption
    fn get_plugin_info(&self) -> Vec<RustFunctionInfo>;
}

/// Information about a Rust function for C# consumption
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RustFunctionInfo {
    pub name: String,
    pub description: String,
    pub wrapper_function_name: String,
    pub schema: String,
    pub requires_permission: bool,
    pub required_permissions: Vec<String>,
}

impl From<&PluginRegistration> for Vec<RustFunctionInfo> {
    fn from(plugin: &PluginRegistration) -> Self {
        plugin.functions.iter().map(|(name, wrapper)| {
            let schema = plugin.schemas.get(name)
                .map(|s| s.to_string())
                .unwrap_or_else(|| "{}".to_string());
            
            RustFunctionInfo {
                name: name.to_string(),
                description: format!("Function: {}", name),
                wrapper_function_name: wrapper.to_string(),
                schema,
                requires_permission: false, // TODO: Parse from plugin metadata
                required_permissions: vec![],
            }
        }).collect()
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AgentConfig {
    pub name: String,
    pub system_instructions: String,
    pub max_function_calls: i32,
    pub max_conversation_history: i32,
    pub provider: Option<ProviderConfig>,
    // Add other fields from C# AgentConfig as needed
    // pub injected_memory: Option<InjectedMemoryConfig>,
    // pub mcp: Option<McpConfig>,
    // pub audio: Option<AudioConfig>,
    // pub web_search: Option<WebSearchConfig>,
    
    /// Plugin-specific configurations for context-aware function filtering.
    /// Key is plugin name, value contains dynamic context properties.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub plugin_configurations: Option<HashMap<String, PluginConfiguration>>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProviderConfig {
    pub provider: ChatProvider,
    pub model_name: String,
    pub api_key: Option<String>,
    pub endpoint: Option<String>,
    // DefaultChatOptions would be complex to serialize, so we'll skip it for now
}

#[derive(Serialize, Clone, Copy)]
#[serde(into = "u32")]
#[repr(u32)]
pub enum ChatProvider {
    OpenAI = 0,
    AzureOpenAI = 1,
    OpenRouter = 2,
    AppleIntelligence = 3,
    Ollama = 4,
}

impl Into<u32> for ChatProvider {
    fn into(self) -> u32 {
        self as u32
    }
}

impl Default for AgentConfig {
    fn default() -> Self {
        Self {
            name: "HPD-Agent".to_string(),
            system_instructions: "You are a helpful assistant.".to_string(),
            max_function_calls: 10,
            max_conversation_history: 20,
            provider: None,
            plugin_configurations: None,
        }
    }
}

pub struct Agent {
    pub(crate) handle: *mut c_void,
}

impl Drop for Agent {
    fn drop(&mut self) {
        // Add a null check for safety
        if !self.handle.is_null() {
            unsafe { ffi::destroy_agent(self.handle) };
            self.handle = std::ptr::null_mut();
        }
    }
}

// Send and Sync are safe because the C# side manages thread safety
unsafe impl Send for Agent {}
unsafe impl Sync for Agent {}

pub struct AgentBuilder {
    config: AgentConfig,
    pending_plugins: Vec<RustFunctionInfo>,
}

impl AgentBuilder {
    pub fn new(name: &str) -> Self {
        Self {
            config: AgentConfig {
                name: name.to_string(),
                ..Default::default()
            },
            pending_plugins: Vec::new(),
        }
    }

    /// Add a plugin to this agent
    /// The plugin will be automatically registered and its functions will be available to the AI
    pub fn with_plugin<P: Plugin + 'static>(mut self, plugin: P) -> Self {
        plugin.register_functions(); // Register with global function registry
        let info = plugin.get_plugin_info(); // Get metadata for C#
        self.pending_plugins.extend(info);
        self
    }

    /// Add multiple plugins from the global registry
    pub fn with_registered_plugins(mut self) -> Self {
        let plugins = crate::plugins::get_registered_plugins();
        for plugin in &plugins {
            let info: Vec<RustFunctionInfo> = plugin.into();
            self.pending_plugins.extend(info);
        }
        self
    }

    pub fn with_instructions(mut self, instructions: &str) -> Self {
        self.config.system_instructions = instructions.to_string();
        self
    }

    pub fn with_max_function_calls(mut self, max_calls: i32) -> Self {
        self.config.max_function_calls = max_calls;
        self
    }

    pub fn with_max_conversation_history(mut self, max_history: i32) -> Self {
        self.config.max_conversation_history = max_history;
        self
    }

    pub fn with_provider(mut self, provider: ProviderConfig) -> Self {
        self.config.provider = Some(provider);
        self
    }

    pub fn with_ollama(mut self, model_name: &str) -> Self {
        self.config.provider = Some(ProviderConfig {
            provider: ChatProvider::Ollama,
            model_name: model_name.to_string(),
            api_key: None,
            endpoint: None,
        });
        self
    }

    pub fn with_ollama_full(mut self, model_name: &str, api_key: Option<String>, endpoint: Option<String>) -> Self {
        self.config.provider = Some(ProviderConfig {
            provider: ChatProvider::Ollama,
            model_name: model_name.to_string(),
            api_key,
            endpoint,
        });
        self
    }

    pub fn with_openai(mut self, model_name: &str, api_key: &str) -> Self {
        self.config.provider = Some(ProviderConfig {
            provider: ChatProvider::OpenAI,
            model_name: model_name.to_string(),
            api_key: Some(api_key.to_string()),
            endpoint: None,
        });
        self
    }

    pub fn with_openrouter(mut self, model_name: &str, api_key: &str) -> Self {
        self.config.provider = Some(ProviderConfig {
            provider: ChatProvider::OpenRouter,
            model_name: model_name.to_string(),
            api_key: Some(api_key.to_string()),
            endpoint: None,
        });
        self
    }

    /// Add a plugin configuration for context-aware behavior.
    /// This enables dynamic function filtering based on runtime context properties.
    pub fn with_plugin_config(mut self, plugin_name: impl Into<String>, config: PluginConfiguration) -> Self {
        if self.config.plugin_configurations.is_none() {
            self.config.plugin_configurations = Some(HashMap::new());
        }
        
        self.config.plugin_configurations
            .as_mut()
            .unwrap()
            .insert(plugin_name.into(), config);
        
        self
    }

    /// Add multiple plugin configurations at once.
    pub fn with_plugin_configs(mut self, configs: HashMap<String, PluginConfiguration>) -> Self {
        if self.config.plugin_configurations.is_none() {
            self.config.plugin_configurations = Some(HashMap::new());
        }
        
        self.config.plugin_configurations
            .as_mut()
            .unwrap()
            .extend(configs);
        
        self
    }

    /// Create a plugin configuration with simple properties and add it to the agent.
    pub fn with_dynamic_plugin_context(self, plugin_name: impl Into<String>, context_type: impl Into<String>, properties: HashMap<String, serde_json::Value>) -> Self {
        let plugin_name = plugin_name.into();
        let config = PluginConfiguration {
            plugin_name: plugin_name.clone(),
            context_type: context_type.into(),
            properties,
            available_functions: None,
        };
        
        self.with_plugin_config(plugin_name, config)
    }

    pub fn build(self) -> Result<Agent, String> {
        let config_json = serde_json::to_string(&self.config)
            .map_err(|e| format!("Failed to serialize config: {}", e))?;
        
        let c_config = CString::new(config_json)
            .map_err(|e| format!("Failed to create CString from config: {}", e))?;
        
        // Serialize the plugins information for C#
        let plugins_json = serde_json::to_string(&self.pending_plugins)
            .map_err(|e| format!("Failed to serialize plugins: {}", e))?;
        
        let c_plugins = CString::new(plugins_json)
            .map_err(|e| format!("Failed to create CString for plugins: {}", e))?;
        
        let agent_handle = unsafe { 
            ffi::create_agent_with_plugins(c_config.as_ptr(), c_plugins.as_ptr()) 
        };
        
        if agent_handle.is_null() {
            Err("Failed to create agent on C# side.".to_string())
        } else {
            Ok(Agent { handle: agent_handle })
        }
    }

    #[cfg(test)]
    pub fn debug_json(&self) -> String {
        serde_json::to_string(&self.config).unwrap_or_default()
    }
}
