using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.OpenRouter;

/// <summary>
/// Auto-discovers and registers the OpenRouter provider on assembly load.
/// </summary>
public static class OpenRouterProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new OpenRouterProvider());
    }
}
