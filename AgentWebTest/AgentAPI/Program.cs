using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using System.Threading.Channels;
using System.Text.Json;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", // Vite dev server
                "http://localhost:4173", // Vite preview
                "http://localhost:3000", // Alternative dev port
                "https://localhost:5173",
                "https://localhost:4173"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add configuration services
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<ProjectManager>(provider => 
    new ProjectManager(
        provider.GetService<ILogger<ProjectManager>>(), 
        provider.GetRequiredService<IConfiguration>()
    ));

var app = builder.Build();

// Enable CORS
app.UseCors("AllowFrontend");

var sampleTodos = new Todo[] {
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
};

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos);
todosApi.MapGet("/{id}", (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? Results.Ok(todo)
        : Results.NotFound());

// Project Management API
var projectsApi = app.MapGroup("/projects").WithTags("Projects");

// GET /projects - List all projects
projectsApi.MapGet("/", (ProjectManager projectManager) =>
{
    try
    {
        var projects = projectManager.ListProjects().Select(p => new ProjectDto(
            p.Id, p.Name, p.Description, p.CreatedAt, p.LastActivity, p.ConversationCount));
        return Results.Ok(projects);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error listing projects", statusCode: 500);
    }
});

// POST /projects - Create new project
projectsApi.MapPost("/", (CreateProjectRequest request, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.CreateProject(request.Name, request.Description);
        var dto = new ProjectDto(project.Id, project.Name, project.Description, 
            project.CreatedAt, project.LastActivity, project.ConversationCount);
        return Results.Created($"/projects/{project.Id}", dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error creating project", statusCode: 500);
    }
});

// GET /projects/{projectId} - Get specific project
projectsApi.MapGet("/{projectId}", (string projectId, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.GetProject(projectId);
        if (project == null)
            return Results.NotFound($"Project {projectId} not found");
            
        var dto = new ProjectDto(project.Id, project.Name, project.Description,
            project.CreatedAt, project.LastActivity, project.ConversationCount);
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error getting project", statusCode: 500);
    }
});

// PUT /projects/{projectId} - Update project
projectsApi.MapPut("/{projectId}", (string projectId, UpdateProjectRequest request, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.GetProject(projectId);
        if (project == null)
            return Results.NotFound($"Project {projectId} not found");
            
        project.Name = request.Name;
        project.Description = request.Description;
        project.UpdateActivity();
        
        var dto = new ProjectDto(project.Id, project.Name, project.Description,
            project.CreatedAt, project.LastActivity, project.ConversationCount);
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error updating project", statusCode: 500);
    }
});

// DELETE /projects/{projectId} - Delete project
projectsApi.MapDelete("/{projectId}", (string projectId, ProjectManager projectManager) =>
{
    try
    {
        var deleted = projectManager.DeleteProject(projectId);
        if (!deleted)
            return Results.NotFound($"Project {projectId} not found");
            
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error deleting project", statusCode: 500);
    }
});

// Conversation Management API
// GET /projects/{projectId}/conversations - List conversations in project
projectsApi.MapGet("/{projectId}/conversations", (string projectId, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.GetProject(projectId);
        if (project == null)
            return Results.NotFound($"Project {projectId} not found");
            
        var conversations = project.Conversations.Select(c => new ConversationDto(
            c.Id, GetConversationDisplayName(c), c.CreatedAt, c.LastActivity, c.Messages.Count));
        return Results.Ok(conversations);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error listing conversations", statusCode: 500);
    }
});

// POST /projects/{projectId}/conversations - Create new conversation
#pragma warning disable IL2026
#pragma warning disable IL2026
projectsApi.MapPost("/{projectId}/conversations", (string projectId, CreateConversationRequest request, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.GetProject(projectId);
        if (project == null)
            return Results.NotFound($"Project {projectId} not found");

        if (project.Agent == null)
            return Results.BadRequest(new ErrorResponse("Project agent not configured"));

        Console.WriteLine($"💬 Creating conversation in project: {project.Id}");
        Console.WriteLine($"🧠 Using existing agent with memory: {project.Agent != null}");

        // Use the project's existing agent (with memory)
        var conversation = project.CreateConversation();

        // Set conversation name if provided
        if (!string.IsNullOrEmpty(request.Name))
        {
            conversation.AddMetadata("DisplayName", request.Name);
        }

        var dto = new ConversationDto(conversation.Id, GetConversationDisplayName(conversation),
            conversation.CreatedAt, conversation.LastActivity, conversation.Messages.Count);
        return Results.Created($"/projects/{projectId}/conversations/{conversation.Id}", dto);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error creating conversation: {ex.Message}");
        return Results.Problem(detail: ex.Message, title: "Error creating conversation", statusCode: 500);
    }
});
#pragma warning restore IL2026
#pragma warning restore IL2026

// GET /projects/{projectId}/conversations/{conversationId} - Get specific conversation
projectsApi.MapGet("/{projectId}/conversations/{conversationId}", (string projectId, string conversationId, ProjectManager projectManager) =>
{
    try
    {
        var conversation = projectManager.GetConversation(projectId, conversationId);
        if (conversation == null)
            return Results.NotFound($"Conversation {conversationId} not found");
            
        var messages = conversation.Messages.Select(m => new MessageDto(
            Guid.NewGuid().ToString(), 
            m.Role.ToString().ToLower(),
            m.Contents.OfType<TextContent>().FirstOrDefault()?.Text ?? "",
            DateTime.UtcNow // You might want to add timestamps to your messages
        ));
        
        var dto = new ConversationWithMessagesDto(
            conversation.Id,
            GetConversationDisplayName(conversation),
            conversation.CreatedAt,
            conversation.LastActivity,
            messages.ToArray()
        );
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error getting conversation", statusCode: 500);
    }
});

// DELETE /projects/{projectId}/conversations/{conversationId} - Delete conversation
projectsApi.MapDelete("/{projectId}/conversations/{conversationId}", (string projectId, string conversationId, ProjectManager projectManager) =>
{
    try
    {
        var project = projectManager.GetProject(projectId);
        if (project == null)
            return Results.NotFound($"Project {projectId} not found");
            
        var deleted = project.RemoveConversation(conversationId);
        if (!deleted)
            return Results.NotFound($"Conversation {conversationId} not found");
            
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Error deleting conversation", statusCode: 500);
    }
});

// Updated Agent API endpoints with project/conversation context
var agentApi = app.MapGroup("/agent").WithTags("Agent");

// Legacy endpoints (for backward compatibility)
#pragma warning disable IL2026
agentApi.MapPost("/chat", async (ChatRequest request, IConfiguration config) =>
{
    try
    {
        var agent = CreateAgent(config);
        if (agent == null)
            return Results.BadRequest(new ErrorResponse("Agent configuration failed"));

        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };
        var response = await agent.GetResponseAsync(messages);
        var textContent = response.Messages.FirstOrDefault()?.Contents.OfType<TextContent>().FirstOrDefault()?.Text;
        
        return Results.Ok(new AgentChatResponse(
            Response: textContent ?? "No response generated",
            Model: response.ModelId ?? "openai/gpt-4o-mini",
            Usage: new UsageInfo(
                InputTokens: response.Usage?.InputTokenCount ?? 0,
                OutputTokens: response.Usage?.OutputTokenCount ?? 0,
                TotalTokens: response.Usage?.TotalTokenCount ?? 0
            )
        ));
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Agent Error", statusCode: 500);
    }
});
#pragma warning restore IL2026

// New context-aware endpoints
#pragma warning disable IL2026
agentApi.MapPost("/projects/{projectId}/conversations/{conversationId}/chat", 
    async (string projectId, string conversationId, ChatRequest request, ProjectManager projectManager, IConfiguration config) =>
{
    try
    {
        var conversation = projectManager.GetConversation(projectId, conversationId);
        if (conversation == null)
            return Results.NotFound($"Conversation {conversationId} not found");

        var response = await conversation.SendAsync(request.Message);
        var textContent = response.Messages.FirstOrDefault()?.Contents.OfType<TextContent>().FirstOrDefault()?.Text;
        
        return Results.Ok(new AgentChatResponse(
            Response: textContent ?? "No response generated",
            Model: response.ModelId ?? "openai/gpt-4o-mini",
            Usage: new UsageInfo(
                InputTokens: response.Usage?.InputTokenCount ?? 0,
                OutputTokens: response.Usage?.OutputTokenCount ?? 0,
                TotalTokens: response.Usage?.TotalTokenCount ?? 0
            )
        ));
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Agent Error", statusCode: 500);
    }
});
#pragma warning restore IL2026

// Context-aware streaming endpoint
#pragma warning disable IL2026
agentApi.MapPost("/projects/{projectId}/conversations/{conversationId}/stream", 
    async (string projectId, string conversationId, StreamRequest request, ProjectManager projectManager, HttpContext context) =>
{
    context.Response.Headers["Access-Control-Allow-Origin"] = "http://localhost:5173";
    context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
    context.Response.Headers["Content-Type"] = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        var conversation = projectManager.GetConversation(projectId, conversationId);
        if (conversation == null)
        {
            await context.Response.WriteAsync("event: error\ndata: {\"error\": \"Conversation not found\"}\n\n");
            return;
        }

        var project = projectManager.GetProject(projectId);
        if (project?.Agent == null)
        {
            await context.Response.WriteAsync("event: error\ndata: {\"error\": \"Project agent not configured\"}\n\n");
            return;
        }

        Console.WriteLine($"🧠 Using project agent with memory: {project.Id}");
        Console.WriteLine($"💬 Conversation: {conversationId}");

        // Get the message from request
        var userMessage = request.Messages.FirstOrDefault()?.Content ?? "";
        Console.WriteLine($"📝 Processing message: {userMessage}");

        // Use conversation's streaming method (it uses the agent with memory)
        var channel = Channel.CreateUnbounded<BaseEvent>();
        var reader = channel.Reader;
        var writer = channel.Writer;

        var streamingTask = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine($"🚀 Starting conversation stream");
                await foreach (var update in conversation.SendStreamingAsync(userMessage))
                {
                    var eventConverter = new AGUIEventConverter();
                    var messageId = Guid.NewGuid().ToString();
                    var agUIEvents = eventConverter.ConvertToAGUIEvents(update, messageId, emitBackendToolCalls: true);
                    foreach (var eventItem in agUIEvents)
                    {
                        await writer.WriteAsync(eventItem);
                    }
                }
                Console.WriteLine($"✅ Stream completed");
                writer.Complete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Stream error: {ex.Message}");
                await writer.WriteAsync(new RunErrorEvent
                {
                    Type = "run_error",
                    Message = ex.Message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                writer.Complete();
            }
        });

        // Stream events to client
        await foreach (var eventItem in reader.ReadAllAsync(context.RequestAborted))
        {
            var eventJson = eventItem switch
            {
                TextMessageContentEvent textEvent => JsonSerializer.Serialize(textEvent, AppJsonSerializerContext.Default.TextMessageContentEvent),
                TextMessageStartEvent startEvent => JsonSerializer.Serialize(startEvent, AppJsonSerializerContext.Default.TextMessageStartEvent),
                TextMessageEndEvent endEvent => JsonSerializer.Serialize(endEvent, AppJsonSerializerContext.Default.TextMessageEndEvent),
                RunStartedEvent runStarted => JsonSerializer.Serialize(runStarted, AppJsonSerializerContext.Default.RunStartedEvent),
                RunFinishedEvent runFinished => JsonSerializer.Serialize(runFinished, AppJsonSerializerContext.Default.RunFinishedEvent),
                RunErrorEvent runError => JsonSerializer.Serialize(runError, AppJsonSerializerContext.Default.RunErrorEvent),
                _ => JsonSerializer.Serialize(eventItem, AppJsonSerializerContext.Default.BaseEvent)
            };
            
            await context.Response.WriteAsync($"event: {eventItem.Type}\ndata: {eventJson}\n\n");
            await context.Response.Body.FlushAsync();
        }

        await streamingTask;
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"event: error\ndata: {{\"error\": \"{ex.Message}\"}}\n\n");
    }
});
#pragma warning restore IL2026

// Context-aware STT endpoint
#pragma warning disable IL2026
agentApi.MapPost("/projects/{projectId}/conversations/{conversationId}/stt", 
    async (string projectId, string conversationId, HttpRequest req, ProjectManager projectManager, IConfiguration config, ILogger<Program> logger) =>
{
    try 
    {
        var conversation = projectManager.GetConversation(projectId, conversationId);
        if (conversation == null)
            return Results.NotFound($"Conversation {conversationId} not found");

        var project = projectManager.GetProject(projectId);
        var agent = CreateAgent(config, project);
        if (agent?.Audio == null)
            return Results.BadRequest(new ErrorResponse("Audio capability not configured"));

        using var ms = new MemoryStream();
        await req.Body.CopyToAsync(ms);
        
        if (ms.Length == 0)
            return Results.BadRequest(new ErrorResponse("Empty audio stream"));
        
        ms.Position = 0;
        var transcript = await agent.Audio.TranscribeAsync(ms);
        
        return Results.Ok(new SttResponse(transcript ?? string.Empty));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "STT processing failed");
        return Results.Text($"STT Error: {ex.Message}", "text/plain", statusCode: 500);
    }
});
#pragma warning restore IL2026

// GET /agent/models - List available models
agentApi.MapGet("/models", () =>
{
    try
    {
        var response = new ModelsResponse(
            Provider: "OpenRouter",
            Models: new[]
            {
                new ModelInfo("openai/gpt-4o-mini", "GPT-4O Mini", "OpenAI"),
                new ModelInfo("openai/gpt-3.5-turbo", "GPT-3.5 Turbo", "OpenAI"),
                new ModelInfo("anthropic/claude-3.5-sonnet", "Claude 3.5 Sonnet", "Anthropic"),
                new ModelInfo("qwen/qwen3-coder:free", "Qwen3 Coder", "Qwen")
            }
        );
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Models Error", statusCode: 500);
    }
});

app.Run();

// Helper functions
static string GetConversationDisplayName(Conversation conversation)
{
    if (conversation.Metadata.TryGetValue("DisplayName", out var displayName))
        return displayName.ToString() ?? $"Chat {conversation.Id[..8]}";
    
    // Generate name from first user message
    var firstUserMessage = conversation.Messages.FirstOrDefault(m => m.Role == ChatRole.User);
    if (firstUserMessage != null)
    {
        var content = firstUserMessage.Contents.OfType<TextContent>().FirstOrDefault()?.Text;
        if (!string.IsNullOrEmpty(content))
        {
            return content.Length > 30 ? content[..30] + "..." : content;
        }
    }
    
    return $"Chat {conversation.Id[..8]}";
}

// Updated helper functions with project context
[RequiresUnreferencedCode("AgentBuilder.Build uses reflection")]
static Agent? CreateAgent(IConfiguration config, Project? project = null)
{
    try
    {
        var builder = AgentBuilder.Create()
            .WithConfiguration(config)
            .WithProvider(ChatProvider.OpenRouter, "google/gemini-2.5-pro")
            .WithName("InteractiveChatAgent")
            .WithInstructions(@"You are an expert AI math assistant. Always be clear, concise, and helpful. Provide code examples when possible. Answer as if you are mentoring a developer.")
            .WithPlugin<MathPlugin>()
            .WithElevenLabsAudio(
                config["ElevenLabs:ApiKey"],
                config["ElevenLabs:DefaultVoiceId"]
            );

        // Add project-specific memory if available
        if (project != null)
        {
            builder.WithMemoryCagCapability(project.AgentMemoryCagManager);
        }

        return builder.Build();
    }
    catch
    {
        return null;
    }
}

[RequiresUnreferencedCode("AgentBuilder.Build uses reflection")]
static Agent? CreateDualInterfaceAgent(IConfiguration config, Project? project = null)
{
    try
    {
        var builder = AgentBuilder.Create()
            .WithConfiguration(config)
            .WithProvider(ChatProvider.OpenRouter, "google/gemini-2.5-pro")
            .WithName("InteractiveChatAgent")
            .WithInstructions(@"You are an expert AI math assistant. Always be clear, concise, and helpful. Provide code examples when possible. Answer as if you are mentoring a developer.")
            .WithPlugin<MathPlugin>()
            .WithFilter(new LoggingAiFunctionFilter())
            .WithMemoryCagCapability(project.AgentMemoryCagManager)
            .WithElevenLabsAudio(
                config["ElevenLabs:ApiKey"],
                config["ElevenLabs:DefaultVoiceId"]
            );

        // Add project-specific memory if available
        if (project != null)
        {
            builder.WithMemoryCagCapability(project.AgentMemoryCagManager);
        }
            
        return builder.Build() as Agent;
    }
    catch
    {
        return null;
    }
}

// Project Manager Service

public class ProjectManager
{
    private readonly ConcurrentDictionary<string, Project> _projects = new();
    private readonly ILogger<ProjectManager> _logger;
    private readonly IConfiguration _configuration;

    public ProjectManager(ILogger<ProjectManager>? logger = null, IConfiguration? configuration = null)
    {
        _logger = logger ?? NullLogger<ProjectManager>.Instance;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
    }

    public Project CreateProject(string name, string description = "")
    {
        var project = new Project(name) { Description = description };
        // Create agent with memory capability for this project
        Console.WriteLine($"🧠 Creating agent with memory for project: {project.Id}");
        var agent = CreateProjectAgent(_configuration, project);
        if (agent != null)
        {
            project.SetAgent(agent);
            Console.WriteLine($"✅ Agent with memory set for project: {project.Id}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to create agent for project: {project.Id}");
        }
        _projects[project.Id] = project;
        _logger.LogInformation("Created project {ProjectId}: {ProjectName}", project.Id, project.Name);
        return project;
    }

    [RequiresUnreferencedCode("AgentBuilder.Build uses reflection")]
    private static Agent? CreateProjectAgent(IConfiguration config, Project project)
    {
        try
        {
            Console.WriteLine($"🔧 Building agent with memory for project: {project.Id}");
            return AgentBuilder.Create()
                .WithConfiguration(config)
                .WithProvider(ChatProvider.OpenRouter, "google/gemini-2.5-pro")
                .WithName("InteractiveChatAgent")
                .WithInstructions(@"You are an expert AI math assistant. Always be clear, concise, and helpful. Provide code examples when possible. Answer as if you are mentoring a developer.")
                .WithMemoryCagCapability(project.AgentMemoryCagManager)
                .WithPlugin<MathPlugin>()
                .WithFilter(new LoggingAiFunctionFilter())
                .WithElevenLabsAudio(
                    config["ElevenLabs:ApiKey"],
                    config["ElevenLabs:DefaultVoiceId"]
                )
                .Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating project agent: {ex.Message}");
            return null;
        }
    }

    public Project? GetProject(string projectId) => _projects.GetValueOrDefault(projectId);
    
    public IEnumerable<Project> ListProjects() => _projects.Values.OrderByDescending(p => p.LastActivity);
    
    public bool DeleteProject(string projectId) 
    {
        var removed = _projects.TryRemove(projectId, out var project);
        if (removed && project != null)
        {
            _logger.LogInformation("Deleted project {ProjectId}: {ProjectName}", projectId, project.Name);
        }
        return removed;
    }
    
    public Conversation? GetConversation(string projectId, string conversationId)
    {
        var project = GetProject(projectId);
        return project?.GetConversation(conversationId);
    }
}

// Data Transfer Objects
public record ProjectDto(string Id, string Name, string Description, DateTime CreatedAt, DateTime LastActivity, int ConversationCount);
public record ConversationDto(string Id, string Name, DateTime CreatedAt, DateTime LastActivity, int MessageCount);
public record ConversationWithMessagesDto(string Id, string Name, DateTime CreatedAt, DateTime LastActivity, MessageDto[] Messages);
public record MessageDto(string Id, string Role, string Content, DateTime Timestamp);

// Request Models
public record CreateProjectRequest(string Name, string Description = "");
public record CreateConversationRequest(string Name = "");
public record UpdateProjectRequest(string Name, string Description);

// Existing models
public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);
public record ChatRequest(string Message);
public record ConversationRequest(ConversationMessage[] Messages);
public record ConversationMessage(string Role, string Content);
public record AgentChatResponse(string Response, string Model, UsageInfo Usage);
public record UsageInfo(long InputTokens, long OutputTokens, long TotalTokens);
public record StreamRequest(string? ThreadId, StreamMessage[] Messages);
public record StreamMessage(string Content);
public record ModelsResponse(string Provider, ModelInfo[] Models);
public record ModelInfo(string Id, string Name, string Provider);
public record SttResponse(string Transcript);
public record ErrorResponse(string Error);