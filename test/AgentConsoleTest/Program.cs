using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using HPD.Agent;

// ═══════════════════════════════════════════════════════════════
// LOGGING SETUP (Required for Console Apps)
// ═══════════════════════════════════════════════════════════════
using var loggerFactory = LoggerFactory.Create(builder =>
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .Build();

    builder
        .AddConsole()
        .AddConfiguration(configuration.GetSection("Logging"));
});

Console.WriteLine("🚀 HPD-Agent Console Test (Core Agent - Direct Access)");

// ✨ ONE-LINER: Create complete AI assistant using CORE agent (not Microsoft adapter)
var result = await CreateAIAssistant(loggerFactory);
var (thread, agent) = result;
if (agent is null) throw new InvalidOperationException("Failed to create AI assistant");

Console.WriteLine($"✅ AI Assistant ready: {agent.Config?.Name ?? "Unknown"}");
Console.WriteLine();

// 🎯 Interactive Chat Loop
await RunInteractiveChat(agent, thread);

// ✨ CONFIG-FIRST APPROACH: Using AgentConfig pattern with AUTO-CONFIGURATION
static Task<(ConversationThread, AgentCore)> CreateAIAssistant(ILoggerFactory loggerFactory)
{
    // ✨ CREATE SERVICE PROVIDER WITH LOGGER FACTORY
    var services = new ServiceCollection();
    services.AddSingleton(loggerFactory);
    var serviceProvider = services.BuildServiceProvider();

    // ✨ CREATE AGENT CONFIG OBJECT FIRST
    var agentConfig = new AgentConfig
    {
        Name = "AI Assistant",
        SystemInstructions = "You are a helpful AI assistant. When you see a Plugin, call it and do not put a paramter inside of it",
        MaxAgenticIterations = 50,  // Set to 2 to test continuation Middleware
        Provider = new ProviderConfig
        {
            ProviderKey = "openrouter",
            ModelName = "google/gemini-2.5-flash", // 🧠 Reasoning model - FREE on OpenRouter!
        },
        DynamicMemory = new DynamicMemoryConfig
        {
            StorageDirectory = "./agent-dynamic-memory",
            MaxTokens = 6000,
            EnableAutoEviction = true,
            AutoEvictionThreshold = 85
        },
        Mcp = new McpConfig
        {
            ManifestPath = "./MCP.json"
        },
        // 📚 Static Memory: Read-only knowledge base (domain expertise, docs, patterns)
        StaticMemory = new StaticMemoryConfig
        {
            StorageDirectory = "./agent-static-memory",
            Strategy = MemoryStrategy.FullTextInjection
        },
        // 🎯 Plugin Scoping: OFF by default (set Enabled = true to enable)
        // When enabled, plugin functions are hidden behind container functions to reduce token usage by up to 87.5%
        // The agent must first call the container (e.g., MathPlugin) before individual functions (Add, Multiply) become visible
        Scoping = new ScopingConfig
        {
            Enabled = true,              // Scope C# plugins (MathPlugin, etc.)      // Scope MCP tools by server (MCP_filesystem, MCP_github, etc.)
            ScopeFrontendTools = false,   // Scope Frontend/AGUI tools (FrontendTools container)
            MaxFunctionNamesInDescription = 10,  // Max function names shown in container descriptions
            SkillInstructionMode = SkillInstructionMode.PromptMiddlewareOnly  // 🎯 Instructions only in system prompt (not in function result)
        },
        // 💭 Reasoning Token Preservation: Controls whether reasoning from models like o1/Gemini is saved in history
        // Default: false (reasoning shown in UI but excluded from history to save tokens/cost)
        // Set to true: Reasoning preserved in conversation history for complex multi-turn scenarios
        PreserveReasoningInHistory = true,  // 🧪 Try setting to true to preserve reasoning tokens!
        // 🐛 Error Handling: Enable detailed error messages for debugging
        ErrorHandling = new ErrorHandlingConfig
        {
            IncludeDetailedErrorsInChat = true  // Show full exception details to help debug permission system issue
        }
    };

    // ✨ CREATE OBSERVER FIRST (without agent reference)
    var eventHandler = new ConsoleEventHandler();

    // ✨ BUILD CORE AGENT WITH OBSERVER - Clean builder pattern!
    var agent = new AgentBuilder(agentConfig)
        .WithLogging()
        .WithPlanMode()  // ✨ Financial analysis plugin (explicitly registered)  // ✨ Financial analysis skills (that reference the plugin)
        .WithPlugin<FinancialAnalysisSkills>()  // ✨ Math plugin (basic math functions
        .WithPlugin<MathPlugin>()  // ✨ MCP tool integration (auto-loads tools from MCP.json manifest)
        .WithPlugin<SpecializedAgents>()  // ✨ Frontend tools plugin (web search, browser, calculator)
        .WithPermissions() // ✨ NEW: Unified permission Middleware - events handled in observer
        .WithObserver(eventHandler)  // ✨ NEW: Register observer via builder API!
        .BuildCoreAgent();  // ✨ Build CORE agent (internal access via InternalsVisibleTo)

    // ✨ SET AGENT REFERENCE (needed for bidirectional events like permissions)
    eventHandler.SetAgent(agent);

    // 💬 Create thread using agent directly
    var thread = agent.CreateThread();

    // ✨ Show config info
    Console.WriteLine($"✨ Agent created with config-first pattern!");
    Console.WriteLine($"📋 Config: {agentConfig.Name} - {agentConfig.Provider?.ModelName}");
    Console.WriteLine($"🧠 Memory: {agentConfig.DynamicMemory?.StorageDirectory}");
    Console.WriteLine($"🔧 Max Function Call Turns: {agentConfig.MaxAgenticIterations}");
    Console.WriteLine($"🎭 Observer Pattern: ConsoleEventHandler registered (handles all events automatically)");

    return Task.FromResult((thread, agent));
}

// 🎯 Interactive Chat Loop - SIMPLIFIED with Observer Pattern!
// All event handling is now done by ConsoleEventHandler observer
static async Task RunInteractiveChat(AgentCore agent, ConversationThread thread)
{
    Console.WriteLine("==========================================");
    Console.WriteLine("🤖 Interactive Chat Mode (Observer Pattern)");
    Console.WriteLine("==========================================");
    Console.WriteLine("Commands:");
    Console.WriteLine("  • Type your message and press Enter");
    Console.WriteLine("  • Press ESC during AI response to stop current turn");
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

            // Create cancellation token source for this turn
            using var cts = new CancellationTokenSource();

            // Start background task to listen for ESC key
            var cancelTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("\n⚠️  Stopping current turn...");
                            Console.ResetColor();
                            cts.Cancel();
                            break;
                        }
                    }
                    Thread.Sleep(50); // Check every 50ms
                }
            });

            try
            {
                // ✨ OBSERVER PATTERN - Just run the agent, NO event loop needed!
                // All events are handled automatically by ConsoleEventHandler observer
                var userMessage = new ChatMessage(ChatRole.User, input);

                // Consume the event stream (observer handles everything)
                await foreach (var _ in agent.RunAsync(
                    new[] { userMessage },
                    options: null,
                    thread: thread,
                    cancellationToken: cts.Token))
                {
                    // Observer handles ALL events - we don't need to do anything here!
                    // This loop just needs to exist to consume the IAsyncEnumerable
                }
            }
            catch (OperationCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n\n🛑 Turn stopped. You can continue the conversation.\n");
                Console.ResetColor();
            }
            finally
            {
                // Signal cancellation task to stop and wait for it
                cts.Cancel();
                await cancelTask;
            }

            // Display message count after each turn
            var messageCount = await thread.GetMessageCountAsync();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n💬 Messages in thread: {messageCount}");
            Console.ResetColor();
            Console.WriteLine(); // Add spacing after response
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}\n");
        }
    }
}
