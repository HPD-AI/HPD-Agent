using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.Mistral;

/// <summary>
/// Auto-discovers and registers the Mistral provider on assembly load.
/// </summary>
public static class MistralProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new MistralProvider());
    }
}
