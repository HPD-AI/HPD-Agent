using Microsoft.KernelMemory;
using System.Collections.Generic;
using System;
using System.IO;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Search;
#pragma warning disable RS1035 // Suppress banned API warnings for file IO and environment variable access

    /// <summary>
    /// Builder for creating agent internal memory (build-time, read-only, optimized).
    /// </summary>
    public class AgentMemoryBuilder
    {
        private readonly IKernelMemoryBuilder _kernelBuilder;
        private readonly string _agentName;
        private readonly AgentMemoryConfig _config;
        // Custom RAG extension points
        private IMemoryDb? _customMemoryDb;
        private ISearchClient? _customSearchClient;

        public AgentMemoryBuilder(string agentName)
        {
            _agentName = agentName;
            _kernelBuilder = new KernelMemoryBuilder();
            _config = new AgentMemoryConfig(agentName);
        }

        // Core configuration methods:
        public AgentMemoryBuilder WithEmbeddingProvider(MemoryEmbeddingProvider provider, string? model = null)
        {
            _config.EmbeddingProvider = provider;
            _config.EmbeddingModel = model;
            return this;
        }

    public AgentMemoryBuilder WithTextGenerationProvider(TextGenerationProvider provider, string? model = null)
        {
            _config.TextGenerationProvider = provider;
            _config.TextGenerationModel = model;
            return this;
        }

        public AgentMemoryBuilder WithStorageOptimization(AgentStorageType storageType)
        {
            _config.StorageType = storageType;
            return this;
        }

        public AgentMemoryBuilder WithReadOnlyOptimization()
        {
            _config.ReadOnlyOptimization = true;
            return this;
        }

        public AgentMemoryBuilder WithDomainContext(IEnumerable<string> domains)
        {
            _config.DomainContexts = domains is string[] arr ? arr : domains.ToArray();
            return this;
        }

        // Content ingestion (build-time):
        public AgentMemoryBuilder WithDocuments(string directoryPath)
        {
            _config.DocumentDirectories.Add(directoryPath);
            return this;
        }

        public AgentMemoryBuilder WithWebSources(IEnumerable<string> urls)
        {
            _config.WebSourceUrls.AddRange(urls);
            return this;
        }

        public AgentMemoryBuilder WithTextContent(Dictionary<string, string> textItems)
        {
            foreach (var kv in textItems)
            {
                _config.TextItems[kv.Key] = kv.Value;
            }
            return this;
        }
        
        // Register a custom memory database implementation
        public AgentMemoryBuilder WithCustomRetrieval(IMemoryDb customMemoryDb)
        {
            _customMemoryDb = customMemoryDb ?? throw new ArgumentNullException(nameof(customMemoryDb));
            return this;
        }

        // Register a custom search client implementation
        public AgentMemoryBuilder WithCustomSearchClient(ISearchClient customSearchClient)
        {
            _customSearchClient = customSearchClient ?? throw new ArgumentNullException(nameof(customSearchClient));
            return this;
        }

        // Register both custom search client and memory database
        public AgentMemoryBuilder WithCustomRAGStrategy(ISearchClient searchClient, IMemoryDb memoryDb)
        {
            return WithCustomSearchClient(searchClient).WithCustomRetrieval(memoryDb);
        }

        /// <summary>
        /// Build and return the configured IKernelMemory instance, applying providers, storage, and ingesting content.
        /// </summary>
        public IKernelMemory Build()
        {
            // Determine default index name
            var defaultIndex = GetAgentIndex();

            // Validate custom RAG configuration
            ValidateCustomImplementations();
            // Apply provider and storage configurations
            ConfigureProviders();
            ConfigureStorage();
            // Register custom RAG components
            RegisterCustomImplementations();

            // Build the memory client
            var memory = _kernelBuilder.Build<MemoryServerless>();

            // Ingest build-time content
            foreach (var dir in _config.DocumentDirectories)
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                {
                    memory.ImportDocumentAsync(file, index: defaultIndex).GetAwaiter().GetResult();
                }
            }
            foreach (var url in _config.WebSourceUrls)
            {
                memory.ImportWebPageAsync(url, index: defaultIndex).GetAwaiter().GetResult();
            }
            foreach (var kv in _config.TextItems)
            {
                memory.ImportTextAsync(kv.Value, documentId: kv.Key, index: defaultIndex).GetAwaiter().GetResult();
            }

            return memory;
        }

        private string GetAgentIndex() => $"{_config.IndexPrefix}-{_config.AgentName.ToLowerInvariant()}";

        // Ensure both custom components are provided together
        private void ValidateCustomImplementations()
        {
            if (_customSearchClient != null && _customMemoryDb == null)
            {
                throw new InvalidOperationException(
                    "Custom search client requires custom memory db. They must work together.");
            }
        }

        // Register custom memory DB and search client into the kernel builder
        private void RegisterCustomImplementations()
        {
            if (_customMemoryDb != null)
            {
                _kernelBuilder.WithCustomMemoryDb(_customMemoryDb);
            }
            if (_customSearchClient != null)
            {
                // Inject custom search client
                _kernelBuilder.AddSingleton<ISearchClient>(_customSearchClient);
            }
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
                    default:
                        throw new NotSupportedException($"Embedding provider {_config.EmbeddingProvider.Value} is not supported.");
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
                case AgentStorageType.InMemory:
                    _kernelBuilder.WithSimpleVectorDb();
                    break;
                case AgentStorageType.SimpleVectorDb:
                    var path = System.IO.Path.Combine("./agent-memory", _config.AgentName);
                    _kernelBuilder.WithSimpleVectorDb(path)
                                  .WithSimpleFileStorage(path);
                    break;
                case AgentStorageType.Qdrant:
                    _kernelBuilder.WithQdrantMemoryDb(GetConfigValue("QDRANT_ENDPOINT"));
                    break;
                case AgentStorageType.AzureAISearch:
                    _kernelBuilder.WithAzureAISearchMemoryDb(GetConfigValue("AZURE_SEARCH_ENDPOINT"), GetApiKey("AZURE_SEARCH_API_KEY"));
                    break;
                case AgentStorageType.Pinecone:
                    throw new NotSupportedException("Pinecone storage is not supported yet.");
                default:
                    throw new NotSupportedException($"Storage type {_config.StorageType} is not supported.");
            }
        }

        private static string GetApiKey(string envVar)
            => Environment.GetEnvironmentVariable(envVar)
               ?? throw new InvalidOperationException($"Environment variable {envVar} not found");

        private static string GetConfigValue(string envVar)
            => Environment.GetEnvironmentVariable(envVar)
               ?? throw new InvalidOperationException($"Environment variable {envVar} not found");
    }

