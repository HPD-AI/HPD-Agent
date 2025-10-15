using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using HPD.Agent.Plugins.FileSystem;

Console.WriteLine("🚀 HPD-Agent Console Test");

// ✨ Load configuration from appsettings.json
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// ✨ ONE-LINER: Create complete AI assistant
var (project, conversation, agent) = await CreateAIAssistant(config);

Console.WriteLine($"✅ AI Assistant ready: {agent.Name}");
Console.WriteLine($"📁 Project: {project.Name}\n");

// 🎯 Interactive Chat Loop
await RunInteractiveChat(conversation);

// ✨ NEW CONFIG-FIRST APPROACH: Using AgentConfig pattern
static Task<(Project, Conversation, Agent)> CreateAIAssistant(IConfiguration config)
{
    // ✨ CREATE AGENT CONFIG OBJECT FIRST
    var agentConfig = new AgentConfig
    {
        Name = "AI Assistant",
        SystemInstructions = "You are a helpful AI assistant with memory, knowledge base, and web search capabilities.",
        MaxAgenticIterations = 10,
        HistoryReduction = new HistoryReductionConfig
        {
            Enabled = true,
            Strategy = HistoryReductionStrategy.MessageCounting,
            TargetMessageCount = 20
        },
        Provider = new ProviderConfig
        {
            ProviderKey = "openrouter",
            ModelName = "google/gemini-2.5-pro", // 🧠 Reasoning model - FREE on OpenRouter!
            // Alternative reasoning models:
            // "deepseek/deepseek-r1-distill-qwen-32b" - smaller/faster
            // "openai/o1" - OpenAI's reasoning model (expensive)
            // No ApiKey here - will use appsettings.json via ResolveApiKey
            DefaultChatOptions = new ChatOptions
            {
                MaxOutputTokens = 4096, // ⚡ Prevents infinite reasoning loops
                Temperature = 0.7f
            }
        },
        DynamicMemory = new DynamicMemoryConfig
        {
            StorageDirectory = "./agent-memory-storage",
            MaxTokens = 6000,
            EnableAutoEviction = true,
            AutoEvictionThreshold = 85
        },
        Mcp = new McpConfig
        {
            ManifestPath = "./MCP.json"
        },
        // 🎯 Plugin Scoping: OFF by default (set Enabled = true to enable)
        // When enabled, plugin functions are hidden behind container functions to reduce token usage by up to 87.5%
        // The agent must first call the container (e.g., MathPlugin) before individual functions (Add, Multiply) become visible
        PluginScoping = new PluginScopingConfig
        {
            Enabled = true,              // Scope C# plugins (MathPlugin, etc.)
            ScopeMCPTools = false,        // Scope MCP tools by server (MCP_filesystem, MCP_github, etc.)
            ScopeFrontendTools = false,   // Scope Frontend/AGUI tools (FrontendTools container)
            MaxFunctionNamesInDescription = 10  // Max function names shown in container descriptions
        }
    };

    // ✨ BUILD AGENT FROM CONFIG + FLUENT PLUGINS/FILTERS
    var agent = new AgentBuilder(agentConfig)
        .WithAPIConfiguration(config) // Pass appsettings.json for API key resolution
        .WithTavilyWebSearch()
        .WithLogging()
        .WithDynamicMemory(opts => opts
            .WithStorageDirectory("./agent-memory-storage")
            .WithMaxTokens(6000))
        .WithPlanMode() // Plan mode enabled with defaults
        .WithPlugin<ExpandMathPlugin>()
        .WithPlugin(new FileSystemPlugin(new FileSystemContext(
            workspaceRoot: Directory.GetCurrentDirectory(),
            enableShell: true, // ✅ Enable shell execution
            maxShellTimeoutSeconds: 60, // 1 minute max timeout
            enableSearch: true,
            respectGitIgnore: true
        )))
        .WithConsolePermissions() // Function permissions only via ConsolePermissionFilter
        .WithMCP(agentConfig.Mcp.ManifestPath)
        .Build();

// 🎯 Project with smart defaults
var project = Project.Create("AI Chat Session");

    // 💬 Conversation just works
    var conversation = project.CreateConversation(agent);

    // ✨ Show config info
    Console.WriteLine($"✨ Agent created with config-first pattern!");
    Console.WriteLine($"📋 Config: {agentConfig.Name} - {agentConfig.Provider?.ModelName}");
    Console.WriteLine($"🧠 Memory: {agentConfig.DynamicMemory?.StorageDirectory}");
    Console.WriteLine($"🔧 Max Function Call Turns: {agentConfig.MaxAgenticIterations}");
    
    return Task.FromResult((project, conversation, agent));
}

// 🎯 Interactive Chat Loop using conversation.RunStreamingAsync
static async Task RunInteractiveChat(Conversation conversation)
{
    Console.WriteLine("==========================================");
    Console.WriteLine("🤖 Interactive Chat Mode");
    Console.WriteLine("==========================================");
    Console.WriteLine("Commands:");
    Console.WriteLine("  • Type your message and press Enter");
    Console.WriteLine("  • 'exit' or 'quit' - End conversation");
    Console.WriteLine("------------------------------------------\n");
    
    while (true)
    {
        Console.Write("You: ");
        var input = Console.ReadLine();
        
        if (input?.ToLower() is "exit" or "quit") 
        {
            Console.WriteLine("👋 Goodbye!");
            break;
        }
        
        if (string.IsNullOrWhiteSpace(input)) continue;

        try
        {
            Console.Write("AI: ");
            
            // Create user message for streaming
            var userMessage = new ChatMessage(ChatRole.User, input);
            
            // Use conversation.RunStreamingAsync to get streaming updates
            await foreach (var update in conversation.RunStreamingAsync([userMessage]))
            {
                // Display different content types from the streaming updates
                foreach (var content in update.Contents ?? [])
                {
                    // Display text content (final answer)
                    if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        Console.Write(textContent.Text);
                    }
                    // Display reasoning content (thinking process) in gray
                    else if (content is TextReasoningContent reasoningContent && !string.IsNullOrEmpty(reasoningContent.Text))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"\n💭 {reasoningContent.Text}");
                        Console.ResetColor();
                    }
                    // Display tool calls
                    else if (content is FunctionCallContent toolCall)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\n🔧 Using tool: {toolCall.Name}");
                        Console.ResetColor();
                    }
                    // Display tool results
                    else if (content is FunctionResultContent toolResult)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($" ✓");
                        Console.ResetColor();
                    }
                }
            }
            
            Console.WriteLine("\n"); // Add spacing after response
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}\n");
        }
    }
}

