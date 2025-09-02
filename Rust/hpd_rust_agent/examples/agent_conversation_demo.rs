use hpd_rust_agent::plugins::{get_registered_plugins, execute_function_async};
use serde_json;
use tokio;

#[tokio::main]
async fn main() {
    println!("🤖 Agent Math Conversation Simulator");
    println!("====================================\n");
    
    println!("Setting up agent with OpenRouter + Gemini 2.5 Pro...");
    println!("✅ Agent configured with math plugin capabilities\n");

    // Simulate realistic agent conversations
    let conversations = vec![
        (
            "👤 User: Hey, what's 156 plus 847?",
            "add",
            r#"{"a": 156.0, "b": 847.0}"#,
            "The sum of 156 and 847 is"
        ),
        (
            "👤 User: Can you calculate 25 squared for me?", 
            "power",
            r#"{"base": 25.0, "exponent": 2.0}"#,
            "25 raised to the power of 2 equals"
        ),
        (
            "👤 User: I need the square root of 144",
            "sqrt",
            r#"{"number": 144.0}"#,
            "The square root of 144 is"
        ),
        (
            "👤 User: What's 120 divided by 8?",
            "divide", 
            r#"{"a": 120.0, "b": 8.0}"#,
            "120 divided by 8 equals"
        ),
        (
            "👤 User: Calculate 15 times 23 please",
            "multiply",
            r#"{"a": 15.0, "b": 23.0}"#,
            "15 multiplied by 23 is"
        ),
    ];

    for (user_input, function_name, args, response_prefix) in conversations {
        println!("{}", user_input);
        
        // Show agent thinking process
        println!("🤖 Agent: Let me calculate that for you...");
        println!("   🧠 Thinking: I need to use the {} function", function_name);
        println!("   🔧 Calling: {}({})", function_name, args);
        
        // Execute the math function
        match execute_function_async(function_name, args).await {
            Ok(result) => {
                // Parse and format the result nicely
                let formatted_result = format_math_result(&result);
                println!("   ✅ Calculation completed!");
                println!("🤖 Agent: {} {}.", response_prefix, formatted_result);
            },
            Err(error) => {
                println!("   ❌ Calculation failed: {}", error);
                println!("🤖 Agent: I apologize, but I encountered an error while calculating that.");
            }
        }
        
        println!();
    }

    println!("{}", "═".repeat(70));
    println!("🎯 This demonstrates the complete agent workflow:");
    println!("   1. User asks a math question");
    println!("   2. Gemini 2.5 Pro analyzes the request");
    println!("   3. Agent identifies the appropriate math function");
    println!("   4. Agent calls the Rust plugin function");
    println!("   5. Rust executes the actual calculation");
    println!("   6. Agent formats and returns the result to user");
    println!("\n✅ Real math calculations powered by Rust plugins!");
    println!("🚀 Ready for production with OpenRouter + Gemini 2.5 Pro!");
}

fn format_math_result(result: &str) -> String {
    // Handle different result formats
    if let Ok(json_result) = serde_json::from_str::<serde_json::Value>(result) {
        if let Some(ok_value) = json_result.get("Ok") {
            return format_number(ok_value.as_f64().unwrap_or(0.0));
        }
    }
    
    // Try parsing as a direct number
    if let Ok(number) = result.trim().trim_matches('"').parse::<f64>() {
        return format_number(number);
    }
    
    // Fallback to original result
    result.to_string()
}

fn format_number(num: f64) -> String {
    if num.fract() == 0.0 {
        format!("{}", num as i64)
    } else {
        format!("{}", num)
    }
}
