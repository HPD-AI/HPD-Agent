using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using Microsoft.KernelMemory;
using HPD_Agent.MemoryRAG;
using System.Threading;

/// <summary>
/// Represents a project containing conversations and scoped memories.
/// </summary>

public class Project
{
    /// <summary>Unique project identifier.</summary>
    public string Id { get; }

    /// <summary>Friendly project name.</summary>
    public string Name { get; set; }

    /// <summary>Project description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; }

    /// <summary>Last activity across all conversations (UTC).</summary>
    public DateTime LastActivity { get; private set; }

    /// <summary>All conversations in this project.</summary>
    public List<Conversation> Conversations { get; } = new();

    /// <summary>Scoped memory manager for this project.</summary>
    public AgentMemoryCagManager AgentMemoryCagManager { get; }

    /// <summary>Document manager for this project</summary>
    public ProjectDocumentManager DocumentManager { get; }

    /// <summary>The agent instance for this project with memory capability</summary>
    public Agent? Agent { get; private set; }

    // Memory management
    private IKernelMemory? _memory;
    private ProjectMemoryBuilder? _memoryBuilder;
    private ProjectDocumentStrategy _documentStrategy = ProjectDocumentStrategy.DirectInjection; // Default to DirectInjection for CAG-only scenarios

    /// <summary>Constructor initializes project, memory and document managers.</summary>
    public Project(string name, string? storageDirectory = null)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = name;
        CreatedAt = DateTime.UtcNow;
        LastActivity = CreatedAt;

        var directory = storageDirectory ?? "./cag-storage";
        AgentMemoryCagManager = new AgentMemoryCagManager(directory);
        AgentMemoryCagManager.SetContext(Id);

        // Initialize document manager with same directory structure
        var textExtractor = new TextExtractionUtility();
        var logger = NullLogger<ProjectDocumentManager>.Instance;
        DocumentManager = new ProjectDocumentManager(directory, textExtractor, logger);
        DocumentManager.SetContext(Id);
    }

    /// <summary>Sets the agent for this project (should be done once)</summary>
    public void SetAgent(Agent agent)
    {
        Agent = agent;
    }

    /// <summary>
    /// Gets or creates the RAG memory instance for this project.
    /// Returns null if no memory builder has been explicitly configured.
    /// </summary>
    /// <returns>The kernel memory instance, or null if not configured</returns>
    public IKernelMemory? GetOrCreateMemory()
    {
        if (_documentStrategy == ProjectDocumentStrategy.DirectInjection)
        {
            return null; // No RAG memory needed for DirectInjection
        }
        
        if (_memory != null) return _memory;
        
        // Only create memory if explicitly configured via SetMemoryBuilder
        if (_memoryBuilder == null)
        {
            return null; // No memory builder configured - return null instead of creating default
        }
        
        var builtMemory = _memoryBuilder.Build();
        if (builtMemory == null)
        {
            throw new InvalidOperationException("Memory builder returned null. This should not happen with RAG strategy.");
        }
        
        return _memory = builtMemory;
    }
    
    /// <summary>
    /// Sets the memory builder for this project
    /// </summary>
    /// <param name="builder">The memory builder to use</param>
    public void SetMemoryBuilder(ProjectMemoryBuilder builder)
    {
        _memoryBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
        _documentStrategy = builder.DocumentStrategy; // Track the strategy
        _memory = null; // Clear existing memory to force rebuild with new builder
    }

    /// <summary>Creates a conversation using the project's agent</summary>
    public Conversation CreateConversation()
    {
        if (Agent == null)
            throw new InvalidOperationException("Agent must be set before creating conversations");

        var conv = new Conversation(Agent);
        conv.AddMetadata("Project", this);
        Conversations.Add(conv);
        UpdateActivity();
        return conv;
    }

    // Keep the old method for backward compatibility
    public Conversation CreateConversation(Agent agent)
    {
        var conv = new Conversation(agent);
        conv.AddMetadata("Project", this);
        Conversations.Add(conv);
        UpdateActivity();
        return conv;
    }

    /// <summary>Update last activity timestamp.</summary>
    public void UpdateActivity() => LastActivity = DateTime.UtcNow;

    // Convenience methods for managing conversations
    /// <summary>Finds a conversation by ID.</summary>
    public Conversation? GetConversation(string conversationId)
        => Conversations.FirstOrDefault(c => c.Id == conversationId);

    /// <summary>Removes a conversation by ID.</summary>
    public bool RemoveConversation(string conversationId)
    {
        var conv = GetConversation(conversationId);
        if (conv != null)
        {
            Conversations.Remove(conv);
            UpdateActivity();
            return true;
        }
        return false;
    }

    /// <summary>Gets the number of conversations.</summary>
    public int ConversationCount => Conversations.Count;

    /// <summary>
    /// Uploads a shared document to the project using the configured strategy.
    /// </summary>
    /// <param name="filePath">Path to the document file</param>
    /// <param name="description">Optional description for the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Project document metadata</returns>
    public async Task<ProjectDocument> UploadDocumentAsync(string filePath, string? description = null, CancellationToken cancellationToken = default)
    {
        switch (_documentStrategy)
        {
            case ProjectDocumentStrategy.RAG:
                var memory = GetOrCreateMemory();
                if (memory != null)
                {
                    await memory.ImportDocumentAsync(filePath, tags: new() { { "project", this.Id } }, cancellationToken: cancellationToken);
                }
                // We still use DocumentManager to track metadata, even in RAG mode
                return await DocumentManager.UploadDocumentAsync(filePath, description, cancellationToken);

            case ProjectDocumentStrategy.DirectInjection:
                // Use the existing ProjectDocumentManager to handle the upload and storage for injection
                return await DocumentManager.UploadDocumentAsync(filePath, description, cancellationToken);
            
            default:
                throw new InvalidOperationException($"Invalid document strategy configured for the project: {_documentStrategy}");
        }
    }

    /// <summary>
    /// Uploads a shared document from URL to the project using the configured strategy.
    /// </summary>
    /// <param name="url">URL of the document to upload</param>
    /// <param name="description">Optional description for the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Project document metadata</returns>
    public async Task<ProjectDocument> UploadDocumentFromUrlAsync(string url, string? description = null, CancellationToken cancellationToken = default)
    {
        switch (_documentStrategy)
        {
            case ProjectDocumentStrategy.RAG:
                var memory = GetOrCreateMemory();
                if (memory != null)
                {
                    await memory.ImportWebPageAsync(url, tags: new() { { "project", this.Id } }, cancellationToken: cancellationToken);
                }
                // We still use DocumentManager to track metadata, even in RAG mode
                return await DocumentManager.UploadDocumentFromUrlAsync(url, description, cancellationToken);

            case ProjectDocumentStrategy.DirectInjection:
                // Use the existing ProjectDocumentManager to handle the upload and storage for injection
                return await DocumentManager.UploadDocumentFromUrlAsync(url, description, cancellationToken);
            
            default:
                throw new InvalidOperationException($"Invalid document strategy configured for the project: {_documentStrategy}");
        }
    }
}

