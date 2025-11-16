namespace HPD.Agent;

/// <summary>
/// Observer that processes agent events for observability purposes.
/// Implementations can log, emit telemetry, cache results, etc.
/// </summary>
public interface IAgentEventObserver
{
    /// <summary>
    /// Determines if this observer should process the given event.
    /// Default: true (process all events).
    /// Override to filter out unwanted events for performance.
    /// </summary>
    /// <param name="evt">The event to potentially process</param>
    /// <returns>True if OnEventAsync should be called, false to skip</returns>
    bool ShouldProcess(InternalAgentEvent evt) => true;

    /// <summary>
    /// Called when an agent emits an event.
    /// Observers should handle events asynchronously without blocking the agent.
    /// </summary>
    Task OnEventAsync(InternalAgentEvent evt, CancellationToken cancellationToken = default);
}
