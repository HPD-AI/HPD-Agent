using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

Console.WriteLine("🚀 HPD-Agent Console Test");

// ✨ Load configuration from appsettings.json
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// ✨ ONE-LINER: Create complete AI assistant
var (project, conversation, agent) = await CreateAIAssistant(config);

Console.WriteLine($"✅ AI Assistant ready: {agent.Name}");
Console.WriteLine($"📁 Project: {project.Name}\n");

// 🧪 TEST: PDF Text Extraction and Injection
Console.WriteLine("🧪 Testing PDF text extraction and injection...");
string? uploadedPdfPath = null;
try
{
    var pdfPath = @"C:\Users\einst\OneDrive\Desktop\Agent\HPD-Agent\AgentConsoleTest\perceptual-maps-best-practice.pdf";
    if (File.Exists(pdfPath))
    {
        Console.WriteLine($"📄 Uploading PDF: {Path.GetFileName(pdfPath)}");
        var document = await project.DocumentManager.UploadDocumentAsync(pdfPath, "Test PDF document");
        Console.WriteLine($"✅ Document uploaded successfully!");
        Console.WriteLine($"   - ID: {document.Id}");
        Console.WriteLine($"   - File: {document.FileName}");
        Console.WriteLine($"   - Size: {document.FileSize:N0} bytes");
        Console.WriteLine($"   - Text Length: {document.ExtractedText.Length:N0} characters");
        Console.WriteLine($"   - First 200 chars: {document.ExtractedText.Substring(0, Math.Min(200, document.ExtractedText.Length))}...\n");

        // Store the path for testing document injection
        uploadedPdfPath = pdfPath;

        // Test document injection in conversation
        Console.WriteLine("🧪 Testing document injection in conversation...");
        Console.WriteLine("Sending test message with PDF document...\n");
        Console.Write("AI: ");
        await StreamResponse(conversation,
            "What is this document about? Give me a brief 2-3 sentence summary of the perceptual maps best practices document.",
            documentPaths: new[] { pdfPath });
        Console.WriteLine("\n");
    }
    else
    {
        Console.WriteLine($"❌ PDF file not found at: {pdfPath}\n");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error testing PDF extraction: {ex.Message}\n");
}

// Debug: list registered tools (plugins + MCP tools)
var registeredTools = agent.DefaultOptions?.Tools;
if (registeredTools != null && registeredTools.Count > 0)
{
    Console.WriteLine("🔧 Registered tools:");
    foreach (var t in registeredTools.OfType<AIFunction>())
    {
        Console.WriteLine($" - {t.Name} : {t.Description}");
    }
}
else
{
    Console.WriteLine("🔧 No registered tools found on the agent.");
}

// 🎯 Simple chat loop
await RunInteractiveChat(conversation);

// ✨ NEW CONFIG-FIRST APPROACH: Using AgentConfig pattern
static Task<(Project, Conversation, Agent)> CreateAIAssistant(IConfiguration config)
{
    // ✨ CREATE AGENT CONFIG OBJECT FIRST
    var agentConfig = new AgentConfig
    {
        Name = "AI Assistant",
        SystemInstructions = "You are a helpful AI assistant with memory, knowledge base, and web search capabilities.",
        MaxFunctionCalls = 6,
        MaxConversationHistory = 20,
        Provider = new ProviderConfig
        {
            Provider = ChatProvider.OpenRouter,
            ModelName = "google/gemini-2.5-pro"
            // No ApiKey here - will use appsettings.json via ResolveApiKey
        },
        InjectedMemory = new InjectedMemoryConfig
        {
            StorageDirectory = "./agent-memory-storage",
            MaxTokens = 6000,
            EnableAutoEviction = true,
            AutoEvictionThreshold = 85
        },
        Mcp = new McpConfig
        {
            ManifestPath = "./MCP.json"
        }
    };

    // ✨ BUILD AGENT FROM CONFIG + FLUENT PLUGINS/FILTERS
    var agent = new AgentBuilder(agentConfig)
        .WithAPIConfiguration(config) // Pass appsettings.json for API key resolution
        .WithTavilyWebSearch()
        .WithLogging()
        .WithInjectedMemory(opts => opts
            .WithStorageDirectory("./agent-memory-storage")
            .WithMaxTokens(6000))
        .WithPlugin<MathPlugin>()
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
    Console.WriteLine($"🧠 Memory: {agentConfig.InjectedMemory?.StorageDirectory}");
    Console.WriteLine($"🔧 Max Function Calls: {agentConfig.MaxFunctionCalls}");
    
    return Task.FromResult((project, conversation, agent));
}

// ✨ CLEAN CHAT LOOP: Fixed response handling
static async Task RunInteractiveChat(Conversation conversation)
{
    Console.WriteLine("==========================================");
    Console.WriteLine("🤖 Interactive Chat Mode");
    Console.WriteLine("==========================================");
    Console.WriteLine("Commands:");
    Console.WriteLine("  • 'exit' or 'quit' - End conversation");
    Console.WriteLine("  • 'audio' - Test audio capabilities");
    Console.WriteLine("  • 'memory' - Show stored memories");
    Console.WriteLine("  • 'remember [text]' - Store a memory");
    Console.WriteLine("------------------------------------------\n");
    
    while (true)
    {
        Console.Write("You: ");
        var input = Console.ReadLine();
        
        if (input?.ToLower() is "exit" or "quit") break;
        if (string.IsNullOrWhiteSpace(input)) continue;

        try
        {
            Console.Write("AI: ");
            
            // 🎯 Handle special commands with streaming
            switch (input.ToLower())
            {
                case "audio":
                    await HandleAudioCommandStreaming(conversation);
                    break;
                case "memory":
                    await StreamResponse(conversation, "Show me my stored memories");
                    break;
                case var cmd when cmd.StartsWith("remember "):
                    await StreamResponse(conversation, $"Please remember this: {input[9..]}");
                    break;
                default:
                    await StreamResponse(conversation, input);
                    break;
            }
            
            Console.WriteLine(); // Add newline after streaming
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}\n");
        }
    }
}

static async Task StreamResponse(Conversation conversation, string message, string[]? documentPaths = null)
{
    // Now returns ConversationStreamingResult with event stream and final metadata
    var result = await conversation.SendStreamingWithOutputAsync(message, documentPaths: documentPaths);
    
    // Display metadata after streaming completes
    if (result.Usage != null)
    {
        Console.Write($" [Tokens: {result.Usage.TotalTokens}");
        if (result.Usage.EstimatedCost.HasValue)
            Console.Write($", Cost: ${result.Usage.EstimatedCost:F4}");
        Console.Write($", Agent: {result.RespondingAgent.Name}");
        Console.Write($", Duration: {result.Duration.TotalSeconds:F1}s]");
    }
}

// ✨ NEW: Streaming audio handler  
static async Task HandleAudioCommandStreaming(Conversation conversation)
{
    Console.Write("Enter audio file path: ");
    var path = Console.ReadLine();
    
    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
    {
        // Direct call with documents using the new consolidated API
        await StreamResponse(conversation, 
            "Please transcribe this audio and provide a helpful response", 
            documentPaths: [path]);
    }
    else
    {
        await StreamResponse(conversation, "No valid audio file provided.");
    }
}