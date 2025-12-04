using System.Runtime.CompilerServices;
using HPD.Providers.Core;

namespace HPD.Providers.OnnxRuntime;

/// <summary>
/// Auto-discovers and registers the ONNX Runtime provider on assembly load.
/// </summary>
public static class OnnxRuntimeProviderModule
{
    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
#pragma warning restore CA2255
    {
        ProviderRegistry.Instance.Register(new OnnxRuntimeProvider());
    }
}
