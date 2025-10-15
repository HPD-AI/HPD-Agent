using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.HuggingFace;

/// <summary>
/// Auto-discovers and registers the HuggingFace provider on assembly load.
/// </summary>
public static class HuggingFaceProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new HuggingFaceProvider());
    }
}
