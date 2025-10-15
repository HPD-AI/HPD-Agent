using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.AzureAIInference;

/// <summary>
/// Auto-discovers and registers the Azure AI Inference provider on assembly load.
/// </summary>
public static class AzureAIInferenceProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new AzureAIInferenceProvider());
    }
}
