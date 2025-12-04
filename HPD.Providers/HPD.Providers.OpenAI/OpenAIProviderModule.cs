using System.Runtime.CompilerServices;
using HPD.Providers.Core;

namespace HPD.Providers.OpenAI;

/// <summary>
/// Auto-discovers and registers OpenAI providers on assembly load.
/// Serves both HPD-Agent (chat) and HPD-Agent.Memory (embeddings).
/// </summary>
public static class OpenAIProviderModule
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
#pragma warning restore CA2255
    {
        // Register with the global provider registry
        ProviderRegistry.Instance.Register(new OpenAIProvider());
        ProviderRegistry.Instance.Register(new AzureOpenAIProvider());
    }
}
