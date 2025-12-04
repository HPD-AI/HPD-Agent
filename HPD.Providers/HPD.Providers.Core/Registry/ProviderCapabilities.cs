// HPD.Providers.Core/Registry/ProviderCapabilities.cs
using System;

namespace HPD.Providers.Core;

/// <summary>
/// Provider capability flags indicating what features a provider supports.
/// A single provider can support multiple capabilities (e.g., OpenAI supports both Chat and Embeddings).
/// </summary>
[Flags]
public enum ProviderCapabilities
{
    /// <summary>
    /// Provider has no capabilities (should not be used in practice).
    /// </summary>
    None = 0,

    // ===== AGENT CAPABILITIES (HPD-Agent) =====

    /// <summary>
    /// Provider supports chat/completion (LLM inference).
    /// </summary>
    Chat = 1 << 0,

    /// <summary>
    /// Provider supports streaming responses.
    /// </summary>
    Streaming = 1 << 1,

    /// <summary>
    /// Provider supports function calling / tool use.
    /// </summary>
    FunctionCalling = 1 << 2,

    /// <summary>
    /// Provider supports vision (image understanding).
    /// </summary>
    Vision = 1 << 3,

    /// <summary>
    /// Provider supports audio processing (speech-to-text, text-to-speech).
    /// </summary>
    Audio = 1 << 4,

    // ===== MEMORY CAPABILITIES (HPD-Agent.Memory) =====

    /// <summary>
    /// Provider can generate text embeddings.
    /// </summary>
    Embeddings = 1 << 10,

    /// <summary>
    /// Provider offers vector storage and similarity search.
    /// </summary>
    VectorStore = 1 << 11,

    /// <summary>
    /// Provider offers document storage (metadata + content).
    /// </summary>
    DocumentStore = 1 << 12,

    /// <summary>
    /// Provider offers graph storage (entities + relationships).
    /// </summary>
    GraphStore = 1 << 13,

    /// <summary>
    /// Provider supports full-text search.
    /// </summary>
    FullTextSearch = 1 << 14,

    // ===== SHARED CAPABILITIES =====

    /// <summary>
    /// Provider supports response caching.
    /// </summary>
    Caching = 1 << 20,

    /// <summary>
    /// Provider supports batch processing.
    /// </summary>
    Batching = 1 << 21,
}
