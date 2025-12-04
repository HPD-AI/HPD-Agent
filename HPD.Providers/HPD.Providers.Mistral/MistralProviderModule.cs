using System.Runtime.CompilerServices;
using HPD.Providers.Core;

namespace HPD.Providers.Mistral;

/// <summary>
/// Auto-discovers and registers the Mistral provider on assembly load.
/// </summary>
public static class MistralProviderModule
{
    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
#pragma warning restore CA2255
    {
        ProviderRegistry.Instance.Register(new MistralProvider());
    }
}
