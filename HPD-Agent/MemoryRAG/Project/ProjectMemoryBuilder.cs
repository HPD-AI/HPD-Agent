using System;
using System.IO;
using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Search;


    /// <summary>
    /// Builder for creating project-scoped memory with persistent storage and multi-user access
    /// </summary>
    public class ProjectMemoryBuilder
    {
        private readonly IKernelMemoryBuilder _kernelBuilder;
        private readonly string _projectId;
        private readonly ProjectMemoryConfig _config;
        // Custom RAG extension points
        private IMemoryDb? _customMemoryDb;
        private ISearchClient? _customSearchClient;

        public ProjectMemoryBuilder(string projectId)
        {
            _projectId = projectId;
            _kernelBuilder = new KernelMemoryBuilder();
            _config = new ProjectMemoryConfig(projectId);
        }

        public ProjectMemoryBuilder WithEmbeddingProvider(MemoryEmbeddingProvider provider, string? model = null)
        {
            _config.EmbeddingProvider = provider;
            _config.EmbeddingModel = model;
            return this;
        }

    public ProjectMemoryBuilder WithTextGenerationProvider(TextGenerationProvider provider, string? model = null)
        {
        _config.TextGenerationProvider = provider;
            _config.TextGenerationModel = model;
            return this;
        }

        public ProjectMemoryBuilder WithStorageOptimization(ProjectStorageType storageType)
        {
            _config.StorageType = storageType;
            return this;
        }

        public ProjectMemoryBuilder WithMultiUserAccess()
        {
            _config.MultiUserAccess = true;
            return this;
        }

        public ProjectMemoryBuilder WithRuntimeManagement()
        {
            _config.RuntimeManagement = true;
            return this;
        }

        public ProjectMemoryBuilder WithProjectContext(string projectId)
        {
            _config.ProjectId = projectId;
            return this;
        }

        public IKernelMemory Build()
        {
            // Validate custom RAG configuration
            ValidateCustomImplementations();
            // Configure providers and storage
            ConfigureProviders();
            ConfigureStorage();
            // Register custom RAG components
            RegisterCustomImplementations();
            return _kernelBuilder.Build<MemoryServerless>();
        }

        // Helper to get config value or environment variable
        private static string? GetConfigValue(string key, string? fallback = null)
        {
            // In a real app, replace with your config provider
            return fallback;
        }
        private static string? GetApiKey(string key)
        {
            // In a real app, replace with your config provider
            return null;
        }

        private void ConfigureProviders()
        {
            // Embedding provider configuration
            if (_config.EmbeddingProvider.HasValue)
            {
                switch (_config.EmbeddingProvider.Value)
                {
                    case MemoryEmbeddingProvider.OpenAI:
                        var openCfg = new OpenAIConfig { APIKey = GetApiKey("OPENAI_API_KEY") ?? string.Empty, EmbeddingModel = _config.EmbeddingModel ?? string.Empty };
                        _kernelBuilder.WithOpenAITextEmbeddingGeneration(openCfg);
                        break;
                    case MemoryEmbeddingProvider.AzureOpenAI:
                        var azCfg = new AzureOpenAIConfig { APIKey = GetApiKey("AZURE_OPENAI_API_KEY") ?? string.Empty, Endpoint = GetConfigValue("AZURE_OPENAI_ENDPOINT") ?? string.Empty, Deployment = _config.EmbeddingModel ?? string.Empty };
                        _kernelBuilder.WithAzureOpenAITextEmbeddingGeneration(azCfg);
                        break;
                    default:
                        throw new NotSupportedException($"Embedding provider {_config.EmbeddingProvider.Value} is not supported.");
                }
            }
            // Text generation provider configuration
            if (_config.TextGenerationProvider.HasValue)
            {
                switch (_config.TextGenerationProvider.Value)
                {
                    case TextGenerationProvider.OpenAI:
                        var oaCfg = new OpenAIConfig { APIKey = GetApiKey("OPENAI_API_KEY") ?? string.Empty, TextModel = _config.TextGenerationModel ?? string.Empty };
                        _kernelBuilder.WithOpenAITextGeneration(oaCfg);
                        break;
                    case TextGenerationProvider.OpenRouter:
                        var orCfg = new OpenRouterConfig { ApiKey = GetApiKey("OPENROUTER_API_KEY") ?? string.Empty, ModelName = _config.TextGenerationModel ?? string.Empty, Endpoint = GetConfigValue("OPENROUTER_ENDPOINT") ?? string.Empty };
                        var orGenerator = new OpenRouterTextGenerator(orCfg, new System.Net.Http.HttpClient(), NullLogger<OpenRouterTextGenerator>.Instance);
                        _kernelBuilder.WithCustomTextGenerator(orGenerator);
                        break;
                    default:
                        throw new NotSupportedException($"Text generation provider {_config.TextGenerationProvider.Value} is not supported.");
                }
            }
        }

        private void ConfigureStorage()
        {
            // Storage configuration based on project settings
            switch (_config.StorageType)
            {
                case ProjectStorageType.Persistent:
                    var path = Path.Combine("./project-memory", _projectId);
                    _kernelBuilder.WithSimpleVectorDb(path)
                                  .WithSimpleFileStorage(path);
                    break;
                default:
                    throw new NotSupportedException($"Storage type {_config.StorageType} is not supported.");
            }
        }
        
        // Register a custom memory database implementation
        public ProjectMemoryBuilder WithCustomRetrieval(IMemoryDb customMemoryDb)
        {
            _customMemoryDb = customMemoryDb ?? throw new ArgumentNullException(nameof(customMemoryDb));
            return this;
        }

        // Register a custom search client implementation
        public ProjectMemoryBuilder WithCustomSearchClient(ISearchClient customSearchClient)
        {
            _customSearchClient = customSearchClient ?? throw new ArgumentNullException(nameof(customSearchClient));
            return this;
        }

        // Register both custom search client and memory database
        public ProjectMemoryBuilder WithCustomRAGStrategy(ISearchClient searchClient, IMemoryDb memoryDb)
        {
            return WithCustomSearchClient(searchClient).WithCustomRetrieval(memoryDb);
        }

        // Ensure custom RAG components are provided together
        private void ValidateCustomImplementations()
        {
            if (_customSearchClient != null && _customMemoryDb == null)
            {
                throw new InvalidOperationException(
                    "Custom search client requires custom memory db. They must work together.");
            }
        }

        // Register custom RAG implementations with the kernel builder
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

