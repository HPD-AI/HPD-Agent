/// <summary>
/// Chat providers for Microsoft.Extensions.AI IChatClient (used in AgentBuilder)
/// </summary>
public enum ChatProvider
{
    // Native Extensions.AI support
    OpenAI,
    AzureOpenAI,
    
    // Custom implementations for Extensions.AI
    OpenRouter,
    AppleIntelligence,
    Ollama
}

/// <summary>
/// Text generation providers for Kernel Memory ITextGenerator (used in Memory RAG builders)
/// </summary>
public enum TextGenerationProvider
{
    // Native Kernel Memory support
    OpenAI,
    AzureOpenAI,
    Ollama,
    Anthropic,
    LlamaSharp,
    ONNX,
    
    // Custom implementations
    OpenRouter,
    
    // Future potential additions
    Cohere,
    HuggingFace
}

/// <summary>
/// Embedding providers for Microsoft.Extensions.AI IEmbeddingGenerator
/// </summary>
public enum AIEmbeddingProvider
{
    // Native Extensions.AI support
    OpenAI,
    AzureOpenAI,

    // Custom or third-party implementations
    VoyageAI,
    Anthropic,
    Cohere,
    HuggingFace,
    ONNX,
    LocalEmbeddings
}

/// <summary>
/// Embedding providers supported by Kernel Memory and agent RAG workflows
/// </summary>
public enum MemoryEmbeddingProvider
{
    // Native Kernel Memory support
    OpenAI,
    AzureOpenAI,
    Ollama,
    
    // Custom implementations  
    VoyageAI,
    
    // Future potential additions
    Anthropic,
    Cohere,
    HuggingFace,
    ONNX,
    LocalEmbeddings  // For SmartComponents.LocalEmbeddings
}

/// <summary>
/// Vector store providers supported by Kernel Memory
/// </summary>
public enum VectorStoreProvider
{
    // Native Kernel Memory support
    InMemory,
    SimpleVectorDb,
    AzureAISearch,
    Qdrant,
    Redis,
    Elasticsearch,
    Postgres,
    SqlServer,
    MongoDBAtlas,

    // Future potential additions
    Pinecone,
    Weaviate,
    Chroma,
    Milvus,
    DuckDB,
    SQLite,
    
    
}

/// <summary>
/// Storage types for Agent Memory (build-time, read-only, optimized)
/// </summary>
public enum AgentStorageType
{
    InMemory,
    SimpleVectorDb,
    Qdrant,
    AzureAISearch,
    Pinecone,
    Postgres,
    Redis,
    Elasticsearch,
    MongoDBAtlas
}

/// <summary>
/// Storage types for Conversation Memory (session-scoped, runtime)
/// </summary>
public enum ConversationStorageType
{
    InMemory,
    SimpleVectorDb,
    Hybrid
}

/// <summary>
/// Storage types for Project Memory (persistent, multi-user)
/// </summary>
public enum ProjectStorageType
{
    Persistent,
    HighAvailability,
    CloudNative
}