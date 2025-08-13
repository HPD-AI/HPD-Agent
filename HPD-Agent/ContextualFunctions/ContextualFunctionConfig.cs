using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Defines fallback behavior when contextual function selection fails
/// </summary>
public enum FallbackMode
{
    UseAllFunctions,    // Safe fallback - agent works normally
    UseNoFunctions,     // Disable function calling for this turn
    ThrowException      // Fail fast for debugging
}

/// <summary>
/// Defines how to truncate context when it exceeds token limits
/// </summary>
public enum ContextTruncationStrategy
{
    KeepRecent,         // Keep most recent messages
    KeepRelevant,       // Keep messages with high keyword overlap (future)
    KeepImportant       // Keep system + user messages, summarize assistant (future)
}

/// <summary>
/// Configuration for contextual function selection system
/// Now leverages Memory RAG infrastructure instead of custom vector stores
/// </summary>
public class ContextualFunctionConfig
{
    // === Simple Provider Configuration ===
    
    /// <summary>
    /// Embedding provider for function similarity search (default: VoyageAI)
    /// </summary>
    public MemoryEmbeddingProvider EmbeddingProvider { get; set; } = MemoryEmbeddingProvider.VoyageAI;
    
    /// <summary>
    /// Vector store provider for function storage (default: InMemory)
    /// </summary>
    public VectorStoreProvider VectorStoreProvider { get; set; } = VectorStoreProvider.InMemory;
    
    /// <summary>
    /// Embedding model name (e.g., "voyage-large-2", "text-embedding-ada-002")
    /// </summary>
    public string? EmbeddingModel { get; set; }
    
    /// <summary>
    /// Configure embedding provider for function descriptions
    /// </summary>
    public ContextualFunctionConfig WithEmbeddingProvider(MemoryEmbeddingProvider provider, string? model = null)
    {
        EmbeddingProvider = provider;
        EmbeddingModel = model;
        return this;
    }
    
    /// <summary>
    /// Configure vector store provider for function storage
    /// </summary>
    public ContextualFunctionConfig WithVectorStoreProvider(VectorStoreProvider provider)
    {
        VectorStoreProvider = provider;
        return this;
    }
    
    // === Core Selection Settings ===
    
    public int MaxRelevantFunctions { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.7f;
    public int RecentMessageWindow { get; set; } = 3;
    
    // === Error Handling ===
    
    public FallbackMode OnEmbeddingFailure { get; set; } = FallbackMode.UseAllFunctions;
    public FallbackMode OnVectorStoreFailure { get; set; } = FallbackMode.UseAllFunctions;
    
    // === Advanced Customization ===
    
    public Func<IEnumerable<ChatMessage>, string>? CustomContextBuilder { get; set; }
    public Func<AIFunction, string>? CustomFunctionDescriptor { get; set; }
}