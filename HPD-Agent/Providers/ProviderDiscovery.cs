// HPD-Agent/Providers/ProviderDiscovery.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HPD.Agent.Providers;

/// <summary>
/// Global discovery mechanism for provider packages.
/// ModuleInitializers register here, AgentBuilder copies to instance registry.
/// </summary>
public static class ProviderDiscovery
{
    private static readonly List<Func<IProviderFeatures>> _factories = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Called by provider package ModuleInitializers to register a provider.
    /// </summary>
    public static void RegisterProviderFactory(Func<IProviderFeatures> factory)
    {
        lock (_lock)
        {
            _factories.Add(factory);
        }
    }

    /// <summary>
    /// Get all discovered provider factories.
    /// Called by AgentBuilder to populate its instance registry.
    /// </summary>
    internal static IEnumerable<Func<IProviderFeatures>> GetFactories()
    {
        lock (_lock)
        {
            return _factories.ToList(); // Return copy for thread safety
        }
    }

    /// <summary>
    /// For testing: clear discovery registry.
    /// </summary>
    internal static void ClearForTesting()
    {
        lock (_lock)
        {
            _factories.Clear();
        }
    }

    /// <summary>
    /// Explicitly loads a provider package to trigger its ModuleInitializer.
    /// Required for Native AOT scenarios where automatic assembly loading is not available.
    /// In non-AOT scenarios, AgentBuilder automatically discovers and loads provider assemblies.
    /// </summary>
    /// <typeparam name="TProviderModule">The provider module type (e.g., HPD_Agent.Providers.OpenRouter.OpenRouterProviderModule)</typeparam>
    /// <example>
    /// <code>
    /// // Native AOT: Explicitly load providers before creating AgentBuilder
    /// ProviderDiscovery.LoadProvider&lt;HPD_Agent.Providers.OpenRouter.OpenRouterProviderModule&gt;();
    /// var agent = new AgentBuilder(config).Build();
    /// </code>
    /// </example>
    public static void LoadProvider<TProviderModule>() where TProviderModule : class
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TProviderModule).Module.ModuleHandle);
    }
}
