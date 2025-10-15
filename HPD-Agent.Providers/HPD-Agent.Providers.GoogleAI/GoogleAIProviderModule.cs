using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.GoogleAI;

/// <summary>
/// Auto-discovers and registers the Google AI provider on assembly load.
/// </summary>
public static class GoogleAIProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new GoogleAIProvider());
    }
}
