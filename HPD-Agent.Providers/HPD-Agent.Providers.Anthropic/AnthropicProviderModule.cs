using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.Anthropic;

/// <summary>
/// Auto-discovers and registers the Anthropic provider on assembly load.
/// </summary>
public static class AnthropicProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new AnthropicProvider());
    }
}
