// HPD-Agent.FFI/ProviderLoader.cs
using System.Runtime.CompilerServices;
using HPD.Agent.Providers;

namespace HPD_Agent.FFI;

/// <summary>
/// Explicitly loads all provider modules to trigger their ModuleInitializers.
/// Required for Native AOT scenarios where assemblies aren't auto-loaded.
/// </summary>
internal static class ProviderLoader
{
    private static bool _loaded = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Load all provider packages that are referenced by this FFI project.
    /// This ensures all providers are available when the native library is loaded.
    /// </summary>
    public static void LoadAllProviders()
    {
        lock (_lock)
        {
            if (_loaded) return;

            // Load each provider module to trigger ModuleInitializer
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.OpenAI.OpenAIProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.Anthropic.AnthropicProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.GoogleAI.GoogleAIProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.AzureAIInference.AzureAIInferenceProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.Bedrock.BedrockProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.Ollama.OllamaProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.Mistral.MistralProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.HuggingFace.HuggingFaceProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.OnnxRuntime.OnnxRuntimeProviderModule).Module.ModuleHandle);
            RuntimeHelpers.RunModuleConstructor(typeof(HPD_Agent.Providers.OpenRouter.OpenRouterProviderModule).Module.ModuleHandle);

            _loaded = true;
        }
    }

    /// <summary>
    /// Module initializer that runs when the FFI library is loaded.
    /// Automatically loads all providers.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        LoadAllProviders();
    }
}
