using System.Runtime.CompilerServices;
using HPD.Providers.Core;

namespace HPD.Providers.OpenRouter;

/// <summary>
/// Auto-discovers and registers the OpenRouter provider on assembly load.
/// </summary>
public static class OpenRouterProviderModule
{
    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
#pragma warning restore CA2255
    {
        ProviderRegistry.Instance.Register(new OpenRouterProvider());
    }
}
