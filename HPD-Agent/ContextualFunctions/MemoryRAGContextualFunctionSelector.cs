using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

/// <summary>
/// Memory RAG-based implementation of contextual function selection using vector similarity search
/// Leverages existing Memory RAG infrastructure instead of custom vector stores
/// </summary>
public class MemoryRAGContextualFunctionSelector : IContextualFunctionSelector
{
    private readonly IKernelMemory _functionMemory;
    private readonly ContextualFunctionConfig _config;
    private readonly Dictionary<string, AIFunction> _functionRegistry = new();
    private readonly Dictionary<string, string> _pluginRegistry = new();
    private readonly ILogger<MemoryRAGContextualFunctionSelector>? _logger;
    private bool _disposed;
    private bool _initialized;
    
    public MemoryRAGContextualFunctionSelector(
        IKernelMemory functionMemory,
        ContextualFunctionConfig config,
        IEnumerable<AIFunction> functions,
        ILogger<MemoryRAGContextualFunctionSelector>? logger = null)
    {
        _functionMemory = functionMemory ?? throw new ArgumentNullException(nameof(functionMemory));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        
        // Build function registry
        foreach (var function in functions)
        {
            _functionRegistry[function.Name] = function;
        }
    }
    
    /// <summary>
    /// Initializes the selector by importing function descriptions into Memory RAG
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;
        
        _logger?.LogInformation("Initializing Memory RAG contextual function selector with {FunctionCount} functions", 
            _functionRegistry.Count);
        
        try
        {
            // Import function descriptions as documents into Memory RAG
            foreach (var kvp in _functionRegistry)
            {
                var function = kvp.Value;
                var description = BuildFunctionDescription(function);
                
                await _functionMemory.ImportTextAsync(
                    description, 
                    documentId: function.Name, 
                    cancellationToken: cancellationToken);
            }
            
            _initialized = true;
            _logger?.LogInformation("Successfully initialized Memory RAG contextual function selector");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Memory RAG contextual function selector");
            throw;
        }
    }
    
    /// <summary>
    /// Registers the plugin type name for a function (called by AgentBuilder)
    /// </summary>
    public void RegisterFunctionPlugin(string functionName, string pluginTypeName)
    {
        _pluginRegistry[functionName] = pluginTypeName;
    }
    
    /// <summary>
    /// Selects the most relevant functions based on conversation context using Memory RAG search
    /// </summary>
    public async Task<IEnumerable<AIFunction>> SelectRelevantFunctionsAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_initialized)
            throw new InvalidOperationException("MemoryRAGContextualFunctionSelector must be initialized before use");
            
        try
        {
            // Build context from recent messages
            var context = BuildContext(messages);
            
            if (string.IsNullOrWhiteSpace(context))
            {
                _logger?.LogDebug("No meaningful context found, using fallback");
                return HandleFallback(_config.OnVectorStoreFailure);
            }
            
            _logger?.LogDebug("Built context for function selection: {Context}", context);
            
            // Use Memory RAG search instead of custom vector store
            var searchResult = await _functionMemory.SearchAsync(
                query: context,
                minRelevance: _config.SimilarityThreshold,
                limit: _config.MaxRelevantFunctions,
                cancellationToken: cancellationToken);
            
            // Map citations back to AIFunction instances
            var relevantFunctions = searchResult.Results
                .Where(citation => _functionRegistry.ContainsKey(citation.DocumentId))
                .Select(citation => _functionRegistry[citation.DocumentId])
                .ToList();
            
            _logger?.LogInformation("Selected {Count} relevant functions from {Total} available (threshold: {Threshold})", 
                relevantFunctions.Count, _functionRegistry.Count, _config.SimilarityThreshold);
            
            return relevantFunctions;
        }
        catch (Exception ex) when (_config.OnVectorStoreFailure != FallbackMode.ThrowException)
        {
            _logger?.LogError(ex, "Error during contextual function selection, using fallback mode: {FallbackMode}", 
                _config.OnVectorStoreFailure);
            return HandleFallback(_config.OnVectorStoreFailure);
        }
    }
    
    /// <summary>
    /// Builds context string from recent conversation messages
    /// </summary>
    private string BuildContext(IEnumerable<ChatMessage> messages)
    {
        var recentMessages = messages
            .TakeLast(3)  // Simple: just use last 3 messages
            .Where(m => m.Role != ChatRole.System && !string.IsNullOrWhiteSpace(m.Text))
            .ToList();
        
        if (recentMessages.Count == 0)
            return string.Empty;
        
        var contextBuilder = new StringBuilder();
        
        foreach (var message in recentMessages)
        {
            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                contextBuilder.AppendLine(message.Text);
            }
        }
        
        return contextBuilder.ToString().Trim();
    }
    
    /// <summary>
    /// Builds a descriptive string for a function to be used for Memory RAG storage
    /// </summary>
    private string BuildFunctionDescription(AIFunction function)
    {
        if (_config.CustomFunctionDescriptor != null)
        {
            return _config.CustomFunctionDescriptor(function);
        }
        
        var description = new StringBuilder();
        
        // Function name
        description.AppendLine($"Function: {function.Name}");
        
        // Function description
        if (!string.IsNullOrWhiteSpace(function.Description))
        {
            description.AppendLine($"Description: {function.Description}");
        }
        
        // Parameter information from JsonSchema
        if (function.JsonSchema.ValueKind == JsonValueKind.Object && 
            function.JsonSchema.TryGetProperty("properties", out var propertiesElement))
        {
            description.AppendLine("Parameters:");
            foreach (var param in propertiesElement.EnumerateObject())
            {
                var paramDescription = "No description";
                if (param.Value.TryGetProperty("description", out var descElement))
                {
                    paramDescription = descElement.GetString() ?? "No description";
                }
                description.AppendLine($"- {param.Name}: {paramDescription}");
            }
        }
        
        return description.ToString().Trim();
    }
    
    /// <summary>
    /// Truncates context based on the specified strategy
    /// </summary>
    private static string TruncateContext(string context, int maxTokens, ContextTruncationStrategy strategy)
    {
        // Simple character-based truncation for now
        // TODO: Implement proper token counting when tokenizer libraries are available
        var maxChars = maxTokens * 4; // Rough approximation: 1 token ≈ 4 characters
        
        if (context.Length <= maxChars)
            return context;
        
        return strategy switch
        {
            ContextTruncationStrategy.KeepRecent => context[^maxChars..],
            ContextTruncationStrategy.KeepRelevant => context[..maxChars], // TODO: Implement keyword-based relevance
            ContextTruncationStrategy.KeepImportant => context[..maxChars], // TODO: Implement importance-based truncation
            _ => context[..maxChars]
        };
    }
    
    /// <summary>
    /// Handles fallback behavior when contextual selection fails
    /// </summary>
    private IEnumerable<AIFunction> HandleFallback(FallbackMode fallbackMode)
    {
        return fallbackMode switch
        {
            FallbackMode.UseAllFunctions => _functionRegistry.Values,
            FallbackMode.UseNoFunctions => Enumerable.Empty<AIFunction>(),
            FallbackMode.ThrowException => throw new InvalidOperationException("Contextual function selection failed and ThrowException fallback mode is configured"),
            _ => _functionRegistry.Values
        };
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryRAGContextualFunctionSelector));
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            // do not dispose IKernelMemory here; it's managed externally
            _disposed = true;
        }
    }
}