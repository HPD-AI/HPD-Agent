using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

/// <summary>
/// Interface for contextual function selection implementations
/// </summary>
public interface IContextualFunctionSelector : IDisposable
{
    /// <summary>
    /// Initializes the selector with the provided functions
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers the plugin type name for a function (called by AgentBuilder)
    /// </summary>
    void RegisterFunctionPlugin(string functionName, string pluginTypeName);
    
    /// <summary>
    /// Selects the most relevant functions based on conversation context
    /// </summary>
    Task<IEnumerable<AIFunction>> SelectRelevantFunctionsAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Clean alias for MemoryRAGContextualFunctionSelector
/// Uses KernelMemory as the vector store provider.
/// </summary>
public class ContextualFunctionSelector : MemoryRAGContextualFunctionSelector
{
    public ContextualFunctionSelector(
        IKernelMemory functionMemory,
        ContextualFunctionConfig config,
        IEnumerable<AIFunction> functions,
        ILogger<ContextualFunctionSelector>? logger = null)
        : base(functionMemory, config, functions, logger as ILogger<MemoryRAGContextualFunctionSelector>)
    {
    }
}