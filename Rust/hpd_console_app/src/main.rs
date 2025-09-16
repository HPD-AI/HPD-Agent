//! HPD Console Application
//! 
//! This is a standalone console application that uses the hpd_rust_agent library
//! as an external dependency. This serves as a real-world test of the library's
//! API and demonstrates how external applications would consume it.

use anyhow::{Context, Result};
use clap::{Parser, Subcommand};
use colored::*;
use hpd_rust_agent::{
    AgentBuilder, 
    Conversation, 
    AppSettings,
    PluginRegistration,
    register_plugin,
    get_registered_plugins,
    hpd_plugin,
    ai_function,
};
use std::io::{self, Write};
use std::collections::HashMap;

#[derive(Parser)]
#[command(name = "hpd-console")]
#[command(about = "A console application demonstrating the HPD Rust Agent Library")]
#[command(version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand)]
enum Commands {
    /// Interactive chat mode
    Chat {
        /// Agent name/persona
        #[arg(short, long, default_value = "assistant")]
        agent: String,
        
        /// Custom instructions for the agent
        #[arg(short, long)]
        instructions: Option<String>,
        
        /// Maximum function calls per turn
        #[arg(short, long, default_value = "5")]
        max_calls: u32,
    },
    /// Test basic functionality
    Test,
    /// Show plugin information
    Plugins,
    /// Run a quick conversation demo
    Demo,
}

// Example plugin for the console app
struct ConsoleUtilities;

impl ConsoleUtilities {
    /// Get the current time in a formatted string
    fn get_current_time(&self) -> String {
        chrono::Utc::now().format("%Y-%m-%d %H:%M:%S UTC").to_string()
    }
    
    /// Calculate simple math operations
    fn calculate(&self, operation: String, a: f64, b: f64) -> Result<f64, String> {
        match operation.as_str() {
            "add" | "+" => Ok(a + b),
            "subtract" | "-" => Ok(a - b),
            "multiply" | "*" => Ok(a * b),
            "divide" | "/" => {
                if b == 0.0 {
                    Err("Cannot divide by zero".to_string())
                } else {
                    Ok(a / b)
                }
            }
            _ => Err(format!("Unknown operation: {}", operation))
        }
    }
    
    /// Echo back a message with console styling
    fn echo_styled(&self, message: String, style: Option<String>) -> String {
        match style.as_deref() {
            Some("red") => message.red().to_string(),
            Some("green") => message.green().to_string(),
            Some("blue") => message.blue().to_string(),
            Some("yellow") => message.yellow().to_string(),
            Some("bold") => message.bold().to_string(),
            _ => message
        }
    }
}

#[tokio::main]
async fn main() -> Result<()> {
    let cli = Cli::parse();
    
    // Initialize the console app
    print_banner();
    
    // Register our console-specific plugin
    register_console_plugin()?;
    
    match cli.command {
        Commands::Chat { agent, instructions, max_calls } => {
            run_interactive_chat(&agent, instructions, max_calls).await?;
        }
        Commands::Test => {
            run_basic_tests().await?;
        }
        Commands::Plugins => {
            show_plugin_info();
        }
        Commands::Demo => {
            run_demo().await?;
        }
    }
    
    Ok(())
}

fn print_banner() {
    println!("{}", "╔══════════════════════════════════════════════════════╗".cyan());
    println!("{}", "║               HPD Console Application                ║".cyan());
    println!("{}", "║          Testing HPD Rust Agent Library             ║".cyan());
    println!("{}", "╚══════════════════════════════════════════════════════╝".cyan());
    println!();
}

fn register_console_plugin() -> Result<()> {
    println!("{} Registering console plugin...", "📦".green());
    
    // Create a simple plugin registration
    let plugin = PluginRegistration {
        name: "console_utilities".to_string(),
        description: "Console utilities for time, math, and text styling".to_string(),
        functions: vec![
            ("get_current_time".to_string(), "get_current_time_wrapper".to_string()),
            ("calculate".to_string(), "calculate_wrapper".to_string()),
            ("echo_styled".to_string(), "echo_styled_wrapper".to_string()),
        ],
        schemas: HashMap::new(), // Would normally contain JSON schemas
    };
    
    register_plugin(plugin);
    
    println!("{} Console plugin registered successfully", "✅".green());
    Ok(())
}

async fn run_interactive_chat(agent_name: &str, instructions: Option<String>, max_calls: u32) -> Result<()> {
    println!("{} Starting interactive chat mode...", "💬".blue());
    
    // Load configuration
    let config = AppSettings::load()
        .map_err(|e| anyhow::anyhow!("Failed to load configuration: {}", e))?;
    
    let api_key = config.get_openrouter_api_key()
        .ok_or_else(|| anyhow::anyhow!("OpenRouter API key not found. Please add it to appsettings.json"))?;
    
    let model = config.get_default_model().unwrap_or("google/gemini-2.5-pro");
    
    // Default instructions
    let default_instructions = format!(
        "You are {}, a helpful AI assistant with access to console utilities. \
         You can help with calculations, get the current time, and echo messages with styling. \
         Be friendly and demonstrate your capabilities when appropriate.",
        agent_name
    );
    
    let agent_instructions = instructions.as_deref().unwrap_or(&default_instructions);
    
    println!("{} Creating agent: {}", "🤖".yellow(), agent_name.bold());
    println!("   Instructions: {}", agent_instructions);
    println!("   Model: {}", model);
    
    // Create agent
    let agent = AgentBuilder::new(agent_name)
        .with_instructions(agent_instructions)
        .with_max_function_calls(max_calls as i32)
        .with_max_conversation_history(50)
        .with_openrouter(model, api_key)
        .build()
        .map_err(|e| anyhow::anyhow!("Failed to create agent: {}", e))?;
    
    // Create conversation
    let conversation = Conversation::new(vec![agent])
        .map_err(|e| anyhow::anyhow!("Failed to create conversation: {}", e))?;
    
    println!("\n{} Chat started! Type 'quit' or 'exit' to end the conversation.", "🎉".green());
    println!("{}", "─".repeat(60).dimmed());
    
    // Interactive loop
    loop {
        print!("\n{} ", "You:".bold().blue());
        io::stdout().flush().unwrap();
        
        let mut input = String::new();
        io::stdin().read_line(&mut input)?;
        let input = input.trim();
        
        if input.is_empty() {
            continue;
        }
        
        if input.eq_ignore_ascii_case("quit") || input.eq_ignore_ascii_case("exit") {
            println!("\n{} Goodbye! Thanks for using HPD Console App!", "👋".yellow());
            break;
        }
        
        print!("{} ", "Assistant:".bold().green());
        io::stdout().flush().unwrap();
        
        match conversation.send(input) {
            Ok(response) => {
                println!("{}", response);
            }
            Err(e) => {
                println!("{} Error: {}", "❌".red(), e.to_string().red());
            }
        }
    }
    
    Ok(())
}

async fn run_basic_tests() -> Result<()> {
    println!("{} Running basic functionality tests...", "🧪".yellow());
    
    // Test 1: Configuration loading
    println!("\n{} Test 1: Configuration Loading", "1️⃣".blue());
    let config = AppSettings::load()
        .map_err(|e| anyhow::anyhow!("Configuration test failed: {}", e))?;
    println!("   ✅ Configuration loaded successfully");
    
    // Test 2: Plugin registration
    println!("\n{} Test 2: Plugin System", "2️⃣".blue());
    let plugins = get_registered_plugins();
    println!("   ✅ Found {} registered plugins", plugins.len());
    for plugin in plugins {
        println!("      - {}", plugin.name.green());
    }
    
    // Test 3: Agent creation (without API call)
    println!("\n{} Test 3: Agent Creation", "3️⃣".blue());
    if let Some(api_key) = config.get_openrouter_api_key() {
        let agent = AgentBuilder::new("test-agent")
            .with_instructions("You are a test agent")
            .with_max_function_calls(1)
            .with_openrouter("google/gemini-2.5-pro", api_key)
            .build()
            .map_err(|e| anyhow::anyhow!("Agent creation test failed: {}", e))?;
        println!("   ✅ Agent created successfully");
    } else {
        println!("   ⚠️  Skipped (no API key configured)");
    }
    
    // Test 4: Conversation creation
    println!("\n{} Test 4: Conversation System", "4️⃣".blue());
    if let Some(api_key) = config.get_openrouter_api_key() {
        let agent = AgentBuilder::new("test-conversation-agent")
            .with_instructions("Test agent for conversation")
            .with_openrouter("google/gemini-2.5-pro", api_key)
            .build()
            .map_err(|e| anyhow::anyhow!("Failed to create agent: {}", e))?;
            
        let conversation = Conversation::new(vec![agent])
            .map_err(|e| anyhow::anyhow!("Conversation creation test failed: {}", e))?;
        println!("   ✅ Conversation created successfully");
    } else {
        println!("   ⚠️  Skipped (no API key configured)");
    }
    
    println!("\n{} All tests completed!", "🎯".green());
    Ok(())
}

fn show_plugin_info() {
    println!("{} Plugin Information", "🔌".purple());
    println!("{}", "─".repeat(30).dimmed());
    
    let plugins = get_registered_plugins();
    
    if plugins.is_empty() {
        println!("No plugins currently registered.");
        return;
    }
    
    println!("Registered plugins ({}):", plugins.len());
    for (i, plugin) in plugins.iter().enumerate() {
        println!("  {}. {}", i + 1, plugin.name.bold());
        println!("     Description: {}", plugin.description);
        println!("     Functions: {}", plugin.functions.len());
    }
    
    // TODO: Add plugin stats if available
    // let stats = get_plugin_stats();
    // println!("\nPlugin Statistics:");
    // println!("  Total function calls: {}", stats.total_calls);
}

async fn run_demo() -> Result<()> {
    println!("{} Running conversation demo...", "🎭".magenta());
    
    let config = AppSettings::load()
        .map_err(|e| anyhow::anyhow!("Failed to load configuration: {}", e))?;
    
    let api_key = config.get_openrouter_api_key()
        .ok_or_else(|| anyhow::anyhow!("API key required for demo"))?;
    
    let model = config.get_default_model().unwrap_or("google/gemini-2.5-pro");
    
    // Create a demo agent
    let agent = AgentBuilder::new("demo-assistant")
        .with_instructions(
            "You are a demo assistant showcasing the HPD Rust Agent Library. \
             You have access to console utilities including math calculations, \
             time functions, and text styling. Demonstrate these capabilities \
             in your responses when appropriate."
        )
        .with_max_function_calls(3)
        .with_openrouter(model, api_key)
        .build()
        .map_err(|e| anyhow::anyhow!("Failed to create demo agent: {}", e))?;
    
    let conversation = Conversation::new(vec![agent])
        .map_err(|e| anyhow::anyhow!("Failed to create demo conversation: {}", e))?;
    
    let demo_messages = vec![
        "Hello! Can you introduce yourself and tell me what time it is?",
        "Can you calculate 15 * 7 + 23 for me?",
        "Show me an example of styled text output in green color",
        "What capabilities do you have available?",
    ];
    
    println!("\n{} Demo conversation starting...", "🚀".green());
    println!("{}", "═".repeat(60).dimmed());
    
    for (i, message) in demo_messages.iter().enumerate() {
        println!("\n{} Demo Message {}: {}", "📤".blue(), i + 1, message.bold());
        
        match conversation.send(message) {
            Ok(response) => {
                println!("{} Response: {}", "📥".green(), response);
            }
            Err(e) => {
                println!("{} Error: {}", "❌".red(), e.to_string().red());
            }
        }
        
        // Pause between messages
        if i < demo_messages.len() - 1 {
            tokio::time::sleep(tokio::time::Duration::from_millis(1000)).await;
        }
    }
    
    println!("\n{} Demo completed successfully!", "🎉".green());
    println!("\n{} Key Demonstrations:", "📋".yellow());
    println!("   • External library consumption");
    println!("   • Agent creation and configuration");
    println!("   • Conversation management");
    println!("   • Plugin system integration");
    println!("   • Error handling and user feedback");
    
    Ok(())
}
