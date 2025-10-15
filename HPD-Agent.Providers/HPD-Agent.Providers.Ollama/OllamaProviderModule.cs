using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.Ollama;

/// <summary>
/// Auto-discovers and registers the Ollama provider on assembly load.
/// </summary>
public static class OllamaProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new OllamaProvider());
    }
}
