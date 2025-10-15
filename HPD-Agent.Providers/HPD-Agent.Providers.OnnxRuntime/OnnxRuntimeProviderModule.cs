using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.Providers.OnnxRuntime;

/// <summary>
/// Auto-discovers and registers the ONNX Runtime provider on assembly load.
/// </summary>
public static class OnnxRuntimeProviderModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ProviderDiscovery.RegisterProviderFactory(() => new OnnxRuntimeProvider());
    }
}
