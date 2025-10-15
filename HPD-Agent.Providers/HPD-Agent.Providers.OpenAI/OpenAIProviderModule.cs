using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.OpenAI;

/// <summary>
/// Auto-discovers and registers OpenAI providers on assembly load.
/// </summary>
public static class OpenAIProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Register with the global discovery registry
        ProviderDiscovery.RegisterProviderFactory(() => new OpenAIProvider());
        ProviderDiscovery.RegisterProviderFactory(() => new AzureOpenAIProvider());
    }
}
