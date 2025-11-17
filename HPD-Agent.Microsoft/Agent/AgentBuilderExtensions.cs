using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using System.Text.Json;

namespace HPD.Agent;

/// <summary>
/// Extension methods for AgentBuilder to support Microsoft protocol agents.
/// </summary>
public static class MicrosoftAgentBuilderExtensions
{
    /// <summary>
    /// Sets the AI context provider factory for Microsoft protocol agents.
    /// The factory is invoked for each new thread to create per-thread AIContextProvider instances.
    /// </summary>
    /// <param name="builder">The agent builder instance</param>
    /// <param name="factory">Factory function that creates AIContextProvider instances</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <remarks>
    /// The factory is invoked for each new thread created via <see cref="Microsoft.Agent.GetNewThread"/>.
    /// For state restoration (deserialization), check <see cref="Microsoft.AIContextProviderFactoryContext.SerializedState"/>.
    /// <para><b>Example - Stateless Provider:</b></para>
    /// <code>
    /// var agent = new AgentBuilder()
    ///     .WithContextProviderFactory(ctx => new MyMemoryProvider())
    ///     .BuildMicrosoftAgent();
    /// </code>
    /// <para><b>Example - Stateful Provider with Restoration:</b></para>
    /// <code>
    /// var agent = new AgentBuilder()
    ///     .WithContextProviderFactory(ctx =>
    ///     {
    ///         // Check if we're restoring from saved state
    ///         if (ctx.SerializedState.ValueKind != JsonValueKind.Undefined &amp;&amp;
    ///             ctx.SerializedState.ValueKind != JsonValueKind.Null)
    ///         {
    ///             return new MyMemoryProvider(ctx.SerializedState, ctx.JsonSerializerOptions);
    ///         }
    ///         return new MyMemoryProvider();
    ///     })
    ///     .BuildMicrosoftAgent();
    /// </code>
    /// </remarks>
    public static AgentBuilder WithContextProviderFactory(
        this AgentBuilder builder,
        Func<Microsoft.AIContextProviderFactoryContext, AIContextProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);

        // Store the factory in builder's internal state
        builder.SetContextProviderFactory(factory);
        return builder;
    }

    /// <summary>
    /// Convenience method for stateless AIContextProvider types.
    /// Creates a new instance for each thread without state restoration.
    /// </summary>
    /// <typeparam name="T">AIContextProvider type with parameterless constructor</typeparam>
    /// <param name="builder">The agent builder instance</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <example>
    /// <code>
    /// var agent = new AgentBuilder()
    ///     .WithContextProvider&lt;MyMemoryProvider&gt;()
    ///     .BuildMicrosoftAgent();
    /// </code>
    /// </example>
    public static AgentBuilder WithContextProvider<T>(this AgentBuilder builder)
        where T : AIContextProvider, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithContextProviderFactory(_ => new T());
    }

    /// <summary>
    /// Convenience method for singleton AIContextProvider (shared across all threads).
    /// </summary>
    /// <param name="builder">The agent builder instance</param>
    /// <param name="provider">Provider instance to share across all threads</param>
    /// <returns>This builder for fluent chaining</returns>
    /// <remarks>
    /// <b>WARNING:</b> Use only for stateless providers or when sharing state is intentional.
    /// All threads will share the same provider instance and its state.
    /// <para>For per-thread isolation, use <see cref="WithContextProviderFactory"/> or <see cref="WithContextProvider{T}"/> instead.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var sharedProvider = new MyStatelessProvider();
    /// var agent = new AgentBuilder()
    ///     .WithSharedContextProvider(sharedProvider)
    ///     .BuildMicrosoftAgent();
    /// </code>
    /// </example>
    public static AgentBuilder WithSharedContextProvider(
        this AgentBuilder builder,
        AIContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(provider);
        return builder.WithContextProviderFactory(_ => provider);
    }

    /// <summary>
    /// Builds a Microsoft protocol agent asynchronously.
    /// Validation behavior is controlled by the ValidationConfig (see WithValidation()).
    /// Returns HPD.Agent.Microsoft.Agent for Microsoft protocol compatibility.
    /// </summary>
    /// <param name="builder">The agent builder instance</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    [RequiresUnreferencedCode("Agent building may use plugin registration methods that require reflection.")]
    public static async Task<Microsoft.Agent> BuildMicrosoftAgentAsync(
        this AgentBuilder builder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Use the builder's internal Build method to get the core agent
        var coreAgent = await builder.BuildCoreAgentAsync(cancellationToken);

        // Get the context provider factory from builder (cast back to expected type)
        var contextProviderFactory = builder.GetContextProviderFactory() as
            Func<Microsoft.AIContextProviderFactoryContext, AIContextProvider>;

        // Wrap in Microsoft protocol adapter
        return new Microsoft.Agent(coreAgent, contextProviderFactory);
    }

    /// <summary>
    /// Builds a Microsoft protocol agent synchronously (blocks thread until complete).
    /// Always uses sync validation for performance.
    /// Returns HPD.Agent.Microsoft.Agent for Microsoft protocol compatibility.
    /// </summary>
    /// <param name="builder">The agent builder instance</param>
    [RequiresUnreferencedCode("Agent building may use plugin registration methods that require reflection.")]
    public static Microsoft.Agent BuildMicrosoftAgent(this AgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Use the builder's internal Build method to get the core agent
        var coreAgent = builder.BuildCoreAgent();

        // Get the context provider factory from builder (cast back to expected type)
        var contextProviderFactory = builder.GetContextProviderFactory() as
            Func<Microsoft.AIContextProviderFactoryContext, AIContextProvider>;

        // Wrap in Microsoft protocol adapter
        return new Microsoft.Agent(coreAgent, contextProviderFactory);
    }
}
