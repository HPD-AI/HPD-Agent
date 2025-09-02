use serde::Deserialize;
use std::fs;

#[derive(Deserialize, Debug)]
pub struct AppSettings {
    #[serde(rename = "OpenRouter")]
    pub open_router: Option<OpenRouterConfig>,
    #[serde(rename = "OpenAI")]
    pub open_ai: Option<OpenAIConfig>,
    #[serde(rename = "Models")]
    pub models: Option<ModelsConfig>,
}

#[derive(Deserialize, Debug)]
pub struct OpenRouterConfig {
    #[serde(rename = "ApiKey")]
    pub api_key: String,
}

#[derive(Deserialize, Debug)]
pub struct OpenAIConfig {
    #[serde(rename = "ApiKey")]
    pub api_key: String,
}

#[derive(Deserialize, Debug)]
pub struct ModelsConfig {
    #[serde(rename = "Default")]
    pub default: String,
    #[serde(rename = "Fallback")]
    pub fallback: String,
}

impl AppSettings {
    pub fn load() -> Result<Self, String> {
        let config_path = "appsettings.json";
        let content = fs::read_to_string(config_path)
            .map_err(|e| format!("Failed to read {}: {}", config_path, e))?;
        
        let settings: AppSettings = serde_json::from_str(&content)
            .map_err(|e| format!("Failed to parse {}: {}", config_path, e))?;
        
        Ok(settings)
    }
    
    pub fn get_openrouter_api_key(&self) -> Option<&str> {
        self.open_router.as_ref().map(|c| c.api_key.as_str())
    }
    
    pub fn get_default_model(&self) -> Option<&str> {
        self.models.as_ref().map(|m| m.default.as_str())
    }
    
    pub fn get_fallback_model(&self) -> Option<&str> {
        self.models.as_ref().map(|m| m.fallback.as_str())
    }
}
