use crate::{hpd_plugin, ai_function};
use serde::{Deserialize, Serialize};

/// Example math plugin that demonstrates the HPD plugin system
#[derive(Debug, Default)]
pub struct MathPlugin {
    pub name: String,
}

impl MathPlugin {
    pub fn new() -> Self {
        Self {
            name: "Advanced Math Plugin".to_string(),
        }
    }
}

#[hpd_plugin("MathPlugin", "A plugin that provides advanced mathematical operations")]
impl MathPlugin {
    #[ai_function("Add two numbers together", name = "add")]
    pub fn add(&self, a: f64, b: f64) -> f64 {
        a + b
    }

    #[ai_function("Subtract two numbers", name = "subtract")]
    pub fn subtract(&self, a: f64, b: f64) -> f64 {
        a - b
    }

    #[ai_function("Multiply two numbers", name = "multiply")]
    pub fn multiply(&self, a: f64, b: f64) -> f64 {
        a * b
    }

    #[ai_function("Divide two numbers", name = "divide")]
    pub fn divide(&self, a: f64, b: f64) -> Result<f64, String> {
        if b == 0.0 {
            Err("Division by zero".to_string())
        } else {
            Ok(a / b)
        }
    }

    #[ai_function("Calculate the power of a number", name = "power")]
    pub fn power(&self, base: f64, exponent: f64) -> f64 {
        base.powf(exponent)
    }

    #[ai_function("Calculate square root", name = "sqrt")]
    pub fn square_root(&self, number: f64) -> Result<f64, String> {
        if number < 0.0 {
            Err("Cannot calculate square root of negative number".to_string())
        } else {
            Ok(number.sqrt())
        }
    }

    #[ai_function("Calculate factorial", name = "factorial")]
    pub fn factorial(&self, n: u64) -> Result<u64, String> {
        if n > 20 {
            return Err("Factorial too large (max 20)".to_string());
        }
        
        let mut result = 1;
        for i in 1..=n {
            result *= i;
        }
        Ok(result)
    }

    #[ai_function("Check if a number is prime", name = "is_prime")]
    pub fn is_prime(&self, number: u64) -> bool {
        if number < 2 {
            return false;
        }
        
        for i in 2..=(number as f64).sqrt() as u64 {
            if number % i == 0 {
                return false;
            }
        }
        true
    }
}

#[derive(Debug, Default)]
pub struct StringPlugin {
    pub operations_count: u32,
}

impl StringPlugin {
    pub fn new() -> Self {
        Self {
            operations_count: 0,
        }
    }
}

#[hpd_plugin("StringPlugin", "A plugin for string manipulation operations")]
impl StringPlugin {
    #[ai_function("Convert string to uppercase", name = "to_upper")]
    pub fn to_uppercase(&mut self, text: String) -> String {
        self.operations_count += 1;
        text.to_uppercase()
    }

    #[ai_function("Convert string to lowercase", name = "to_lower")]
    pub fn to_lowercase(&mut self, text: String) -> String {
        self.operations_count += 1;
        text.to_lowercase()
    }

    #[ai_function("Reverse a string", name = "reverse")]
    pub fn reverse(&mut self, text: String) -> String {
        self.operations_count += 1;
        text.chars().rev().collect()
    }

    #[ai_function("Count characters in a string", name = "char_count")]
    pub fn character_count(&mut self, text: String) -> usize {
        self.operations_count += 1;
        text.chars().count()
    }

    #[ai_function("Split string by delimiter", name = "split")]
    pub fn split_string(&mut self, text: String, delimiter: String) -> Vec<String> {
        self.operations_count += 1;
        text.split(&delimiter).map(|s| s.to_string()).collect()
    }

    #[ai_function("Get operations count", name = "get_count")]
    pub fn get_operations_count(&self) -> u32 {
        self.operations_count
    }

    #[ai_function("Reset operations counter", name = "reset_count")]
    pub fn reset_counter(&mut self) -> String {
        self.operations_count = 0;
        "Counter reset to 0".to_string()
    }
}

#[derive(Debug, Default, Serialize, Deserialize)]
pub struct AsyncPlugin {
    pub request_count: u64,
}

impl AsyncPlugin {
    pub fn new() -> Self {
        Self { request_count: 0 }
    }
}

#[hpd_plugin("AsyncPlugin", "Demonstrates async AI functions with external requests")]
impl AsyncPlugin {
    #[ai_function("Simulate async computation", name = "async_compute")]
    pub async fn async_compute(&mut self, duration_ms: u64) -> String {
        self.request_count += 1;
        
        tokio::time::sleep(tokio::time::Duration::from_millis(duration_ms)).await;
        
        format!("Async computation completed after {}ms (request #{})", 
                duration_ms, self.request_count)
    }

    #[ai_function("Get current timestamp", name = "timestamp")]
    pub async fn get_timestamp(&mut self) -> String {
        self.request_count += 1;
        
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs();
            
        format!("Current timestamp: {} (request #{})", now, self.request_count)
    }

    #[ai_function("Simulate network request", name = "network_request")]
    pub async fn simulate_network_request(&mut self, url: String) -> Result<String, String> {
        self.request_count += 1;
        
        // Simulate network delay
        tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;
        
        if url.starts_with("https://") {
            Ok(format!("Successfully fetched data from {} (request #{})", 
                      url, self.request_count))
        } else {
            Err("Invalid URL: must start with https://".to_string())
        }
    }

    #[ai_function("Get request statistics", name = "get_stats")]
    pub fn get_request_stats(&self) -> serde_json::Value {
        serde_json::json!({
            "total_requests": self.request_count,
            "plugin_name": "AsyncPlugin",
            "status": "active"
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{get_registered_plugins, get_plugin_stats};

    #[tokio::test]
    async fn test_math_plugin() {
        let math = MathPlugin::new();
        
        assert_eq!(math.add(2.0, 3.0), 5.0);
        assert_eq!(math.multiply(4.0, 5.0), 20.0);
        assert_eq!(math.power(2.0, 3.0), 8.0);
        assert!(math.is_prime(17));
        assert!(!math.is_prime(16));
    }

    #[tokio::test]
    async fn test_string_plugin() {
        let mut strings = StringPlugin::new();
        
        assert_eq!(strings.to_uppercase("hello".to_string()), "HELLO");
        assert_eq!(strings.reverse("world".to_string()), "dlrow");
        assert_eq!(strings.character_count("test".to_string()), 4);
        assert_eq!(strings.get_operations_count(), 3);
    }

    #[tokio::test]
    async fn test_async_plugin() {
        let mut async_plugin = AsyncPlugin::new();
        
        let result = async_plugin.async_compute(100).await;
        assert!(result.contains("100ms"));
        assert!(result.contains("request #1"));
        
        let timestamp = async_plugin.get_timestamp().await;
        assert!(timestamp.contains("Current timestamp"));
        assert!(timestamp.contains("request #2"));
    }

    #[test]
    fn test_plugin_registration() {
        // Check that plugins were automatically registered
        let plugins = get_registered_plugins();
        println!("Registered plugins: {:?}", plugins.iter().map(|p| &p.name).collect::<Vec<_>>());
        
        // Verify we have the expected plugins
        let plugin_names: Vec<&String> = plugins.iter().map(|p| &p.name).collect();
        assert!(plugin_names.contains(&&"MathPlugin".to_string()));
        assert!(plugin_names.contains(&&"StringPlugin".to_string()));
        assert!(plugin_names.contains(&&"AsyncPlugin".to_string()));
        
        // Check plugin stats
        let stats = get_plugin_stats();
        println!("Plugin stats: {}", serde_json::to_string_pretty(&stats).unwrap());
    }

    #[test]
    fn test_plugin_schemas() {
        let plugins = get_registered_plugins();
        
        for plugin in plugins {
            println!("Plugin: {}", plugin.name);
            println!("Description: {}", plugin.description);
            println!("Functions: {:?}", plugin.functions);
            
            for (func_name, schema_str) in plugin.schemas {
                println!("  Function: {}", func_name);
                if let Ok(schema) = serde_json::from_str::<serde_json::Value>(&schema_str) {
                    println!("  Schema: {}", serde_json::to_string_pretty(&schema).unwrap());
                }
            }
        }
    }
}
