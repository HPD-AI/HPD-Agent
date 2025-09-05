use std::collections::HashMap;
use serde_json::json;
use hpd_rust_agent::{AgentBuilder, PluginConfiguration, PluginContext};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("=== HPD-Agent Dynamic Plugin Configuration Example ===");
    
    // Create a plugin configuration for a WebSearch plugin
    let mut web_search_properties = HashMap::new();
    web_search_properties.insert("provider".to_string(), json!("Tavily"));
    web_search_properties.insert("maxResults".to_string(), json!(10));
    web_search_properties.insert("hasPermission".to_string(), json!(true));
    web_search_properties.insert("enableImageSearch".to_string(), json!(false));
    
    let web_search_config = PluginConfiguration::new("WebSearchPlugin", "WebSearchPluginMetadataContext")
        .with_property("provider", "Tavily")?
        .with_property("maxResults", 10)?
        .with_property("hasPermission", true)?
        .with_available_functions(vec![
            "search".to_string(),
            "search_news".to_string()
        ]);
    
    println!("✅ Created WebSearch plugin configuration:");
    println!("   Plugin: {}", web_search_config.plugin_name);
    println!("   Context Type: {}", web_search_config.context_type);
    println!("   Properties: {} items", web_search_config.properties.len());
    println!("   Available Functions: {:?}", web_search_config.available_functions);
    
    // Create another plugin configuration for Math operations
    let math_config = PluginConfiguration::new("MathPlugin", "MathPluginMetadataContext")
        .with_property("allowNegative", true)?
        .with_property("maxValue", 1000000)?
        .with_property("precision", 2)?;
    
    println!("\n✅ Created Math plugin configuration:");
    println!("   Plugin: {}", math_config.plugin_name);
    println!("   Context Type: {}", math_config.context_type);
    
    // Build an agent with these plugin configurations
    let agent_builder = AgentBuilder::new("context-aware-agent")
        .with_instructions("You are an intelligent agent with context-aware plugins.")
        .with_plugin_config("WebSearchPlugin", web_search_config)
        .with_plugin_config("MathPlugin", math_config);
    
    // Alternatively, create a simple plugin context using the convenience method
    let mut chat_properties = HashMap::new();
    chat_properties.insert("model".to_string(), json!("gpt-4"));
    chat_properties.insert("temperature".to_string(), json!(0.7));
    chat_properties.insert("maxTokens".to_string(), json!(2000));
    
    let agent_builder = agent_builder.with_dynamic_plugin_context(
        "ChatPlugin", 
        "ChatPluginMetadataContext", 
        chat_properties
    );
    
    println!("\n✅ Built agent with context-aware plugin configurations");
    
    // Test the PluginContext utility
    let mut context = PluginContext::new();
    context.set_property("testString", "Hello World")?;
    context.set_property("testNumber", 42)?;
    context.set_property("testBool", true)?;
    
    println!("\n✅ Created and populated PluginContext:");
    println!("   String property: {:?}", context.get_property::<String>("testString"));
    println!("   Number property: {:?}", context.get_property::<i32>("testNumber"));
    println!("   Boolean property: {:?}", context.get_property::<bool>("testBool"));
    println!("   Has 'testString': {}", context.has_property("testString"));
    println!("   Has 'nonexistent': {}", context.has_property("nonexistent"));
    
    // Test JSON serialization
    let context_json = context.to_json()?;
    println!("\n✅ Context as JSON: {}", context_json);
    
    let deserialized_context = PluginContext::from_json(&context_json)?;
    println!("   Deserialized successfully: {} properties", deserialized_context.property_names().len());
    
    println!("\n🎉 Phase 1 implementation complete!");
    println!("   ✅ PluginConfiguration class created with JSON serialization");
    println!("   ✅ AgentConfig extended with plugin configurations");
    println!("   ✅ CreateAgentWithPlugins updated to process configurations");
    println!("   ✅ AgentBuilder enhanced with configuration methods");
    println!("   ✅ DynamicPluginMetadataContext provides runtime property access");
    println!("   ✅ Full integration with existing HPDJsonContext");
    
    Ok(())
}