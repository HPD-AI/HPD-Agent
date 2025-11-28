namespace HPD.Agent;

/// <summary>
/// Friendly alias for <see cref="IAgentEventObserver"/>.
/// Implements the Observer pattern for handling agent events.
/// Use whichever name you prefer - both interfaces are equivalent.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a semantic alias that makes the API more intuitive for developers
/// who prefer "handler" terminology over "observer" terminology. Both refer to the same concept.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// public class MyEventHandler : IEventHandler
/// {
///     public bool ShouldProcess(AgentEvent evt)
///     {
///         return evt is PermissionRequestEvent or TextDeltaEvent;
///     }
///
///     public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
///     {
///         switch (evt)
///         {
///             case PermissionRequestEvent permReq:
///                 await HandlePermissionAsync(permReq, ct);
///                 break;
///             case TextDeltaEvent textDelta:
///                 Console.Write(textDelta.Text);
///                 break;
///         }
///     }
/// }
///
/// // Register with agent
/// var agent = new AgentBuilder(config)
///     .WithObserver(new MyEventHandler())
///     .BuildCoreAgent();
/// </code>
/// </para>
/// </remarks>
public interface IEventHandler : IAgentEventObserver
{
    // Intentionally empty - this is just a friendly alias for IAgentEventObserver
    // All functionality comes from the base interface
}
