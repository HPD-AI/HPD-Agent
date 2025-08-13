using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Search;



/// <summary>
/// Builder for creating conversation-scoped memory for pure RAG usage.
/// </summary>
public class ConversationMemoryBuilder
{
    private readonly IKernelMemoryBuilder _kernelBuilder;
    private readonly string _conversationId;
    private readonly ConversationMemoryConfig _config;

    // Custom RAG extension points
    private IMemoryDb? _customMemoryDb;
    private ISearchClient? _customSearchClient;

    public ConversationMemoryBuilder(string conversationId)
    {
        _conversationId = conversationId;
        _kernelBuilder = new KernelMemoryBuilder();
        _config = new ConversationMemoryConfig(conversationId);
    }

    // Core configuration:
    public ConversationMemoryBuilder WithEmbeddingProvider(MemoryEmbeddingProvider provider, string? model = null)
    {
        _config.EmbeddingProvider = provider;
        _config.EmbeddingModel = model;
        return this;
    }

    public ConversationMemoryBuilder WithTextGenerationProvider(TextGenerationProvider provider, string? model = null)
    {
    _config.TextGenerationProvider = provider;
        _config.TextGenerationModel = model;
        return this;
    }

    public ConversationMemoryBuilder WithStorageOptimization(ConversationStorageType storageType)
    {
        _config.StorageType = storageType;
        return this;
    }

    public ConversationMemoryBuilder WithContextWindowOptimization()
    {
        _config.ContextWindowOptimization = true;
        return this;
    }

    public ConversationMemoryBuilder WithFastRetrieval()
    {
        _config.FastRetrieval = true;
        return this;
    }

    // Register a custom memory database implementation
    public ConversationMemoryBuilder WithCustomRetrieval(IMemoryDb customMemoryDb)
    {
        _customMemoryDb = customMemoryDb ?? throw new ArgumentNullException(nameof(customMemoryDb));
        return this;
    }

    // Register a custom search client implementation
    public ConversationMemoryBuilder WithCustomSearchClient(ISearchClient customSearchClient)
    {
        _customSearchClient = customSearchClient ?? throw new ArgumentNullException(nameof(customSearchClient));
        return this;
    }

    // Register both custom search client and memory database
    public ConversationMemoryBuilder WithCustomRAGStrategy(ISearchClient searchClient, IMemoryDb memoryDb)
    {
        return WithCustomSearchClient(searchClient).WithCustomRetrieval(memoryDb);
    }

    /// <summary>
    /// Build the configured IKernelMemory for RAG usage.
    /// </summary>
    public IKernelMemory Build()
    {
        ValidateCustomImplementations();
        ConfigureProviders();
        ConfigureStorage();
        RegisterCustomImplementations();
        return _kernelBuilder.Build<MemoryServerless>();
    }

    private void ConfigureProviders()
    {
        // Embedding providers
        if (_config.EmbeddingProvider.HasValue)
        {
            switch (_config.EmbeddingProvider.Value)
            {
                case MemoryEmbeddingProvider.OpenAI:
                    var openCfg = new Microsoft.KernelMemory.OpenAIConfig
                    {
                        APIKey = GetApiKey("OPENAI_API_KEY"),
                        EmbeddingModel = _config.EmbeddingModel ?? "text-embedding-ada-002"
                    };
                    _kernelBuilder.WithOpenAITextEmbeddingGeneration(openCfg);
                    break;
                case MemoryEmbeddingProvider.AzureOpenAI:
                    var azCfg = new Microsoft.KernelMemory.AzureOpenAIConfig
                    {
                        APIKey = GetApiKey("AZURE_OPENAI_API_KEY"),
                        Endpoint = GetConfigValue("AZURE_OPENAI_ENDPOINT"),
                        Deployment = _config.EmbeddingModel ?? "text-embedding-ada-002"
                    };
                    _kernelBuilder.WithAzureOpenAITextEmbeddingGeneration(azCfg);
                    break;
                case MemoryEmbeddingProvider.VoyageAI:
                    var voyageConfig = new VoyageAIConfig
                    {
                        ApiKey = GetApiKey("VOYAGEAI_API_KEY"),
                        ModelName = _config.EmbeddingModel ?? "voyage-large-2",
                        Endpoint = GetConfigValue("VOYAGEAI_ENDPOINT") ?? "https://api.voyageai.com/v1/embeddings"
                    };
                    var voyageGenerator = new VoyageAITextEmbeddingGenerator(voyageConfig, new HttpClient(),
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<VoyageAITextEmbeddingGenerator>.Instance);
                    _kernelBuilder.WithCustomEmbeddingGenerator(voyageGenerator);
                    break;
            }
        }

        // Text generation providers
    if (_config.TextGenerationProvider.HasValue)
        {
            switch (_config.TextGenerationProvider.Value)
            {
                case TextGenerationProvider.OpenAI:
                    var oaCfg = new Microsoft.KernelMemory.OpenAIConfig
                    {
                        APIKey = GetApiKey("OPENAI_API_KEY"),
                        TextModel = _config.TextGenerationModel ?? "gpt-3.5-turbo"
                    };
                    _kernelBuilder.WithOpenAITextGeneration(oaCfg);
                    break;
                case TextGenerationProvider.OpenRouter:
                    var openRouterConfig = new OpenRouterConfig
                    {
                        ApiKey = GetApiKey("OPENROUTER_API_KEY"),
                        ModelName = _config.TextGenerationModel ?? "anthropic/claude-3.5-sonnet",
                        Endpoint = GetConfigValue("OPENROUTER_ENDPOINT") ?? "https://openrouter.ai/api/v1/chat/completions"
                    };
                    var openRouterGenerator = new OpenRouterTextGenerator(openRouterConfig, new HttpClient(),
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenRouterTextGenerator>.Instance);
                    _kernelBuilder.WithCustomTextGenerator(openRouterGenerator);
                    break;
            }
        }
    }

    private void ConfigureStorage()
    {
        switch (_config.StorageType)
        {
            case ConversationStorageType.InMemory:
                _kernelBuilder.WithSimpleVectorDb();
                break;
            case ConversationStorageType.SimpleVectorDb:
                var path = Path.Combine("./conversation-memory", _config.ConversationId);
                _kernelBuilder.WithSimpleVectorDb(path)
                              .WithSimpleFileStorage(path);
                break;
            case ConversationStorageType.Hybrid:
                _kernelBuilder.WithSimpleVectorDb();
                break;
        }
    }

    private static string GetApiKey(string envVar)
    {
        return $"placeholder-{envVar}";
    }

    private static string GetConfigValue(string envVar)
    {
        return $"placeholder-{envVar}";
    }

    private void ValidateCustomImplementations()
    {
        if (_customSearchClient != null && _customMemoryDb == null)
        {
            throw new InvalidOperationException("Custom search client requires custom memory db. They must work together.");
        }
    }

    private void RegisterCustomImplementations()
    {
        if (_customMemoryDb != null)
        {
            _kernelBuilder.WithCustomMemoryDb(_customMemoryDb);
        }
        if (_customSearchClient != null)
        {
            _kernelBuilder.AddSingleton<ISearchClient>(_customSearchClient);
        }
    }
}

