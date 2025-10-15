using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.Bedrock;

/// <summary>
/// Auto-discovers and registers the AWS Bedrock provider on assembly load.
/// </summary>
public static class BedrockProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new BedrockProvider());
    }
}
