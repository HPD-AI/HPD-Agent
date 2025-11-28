namespace HPD.Agent;

/// <summary>
/// Abstraction for bidirectional event coordination.
/// Enables middlewares to emit events and wait for responses
/// without knowing about AgentCore internals.
/// </summary>
/// <remarks>
/// <para>
/// This interface decouples middleware from AgentCore, enabling:
/// - Clean middleware architecture (no agent reference needed)
/// - Easy unit testing (mock the interface)
/// - Future implementations (e.g., distributed event coordination)
/// </para>
/// <para>
/// <b>Threading:</b> All methods must be thread-safe. Multiple middlewares
/// can emit events concurrently.
/// </para>
/// <para>
/// <b>Event Bubbling:</b> Implementations should support event bubbling
/// for nested agent scenarios (child agent events visible to parent).
/// </para>
/// </remarks>
public interface IEventCoordinator
{
    /// <summary>
    /// Emits an event to handlers. Fire-and-forget.
    /// Events bubble to parent coordinators in nested agent scenarios.
    /// </summary>
    /// <param name="evt">The event to emit</param>
    /// <exception cref="ArgumentNullException">If event is null</exception>
    /// <remarks>
    /// <para>
    /// This is the primary way for middlewares to communicate with external handlers.
    /// Events are written to a channel and processed asynchronously.
    /// </para>
    /// <para>
    /// <b>Thread-safe:</b> Can be called from any thread.
    /// </para>
    /// <para>
    /// <b>Non-blocking:</b> Returns immediately (unbounded channel).
    /// </para>
    /// </remarks>
    void Emit(AgentEvent evt);

    /// <summary>
    /// Sends a response to a waiting request.
    /// Called by handlers when user provides input.
    /// </summary>
    /// <param name="requestId">The unique identifier for the request</param>
    /// <param name="response">The response event to deliver</param>
    /// <exception cref="ArgumentNullException">If response is null</exception>
    /// <remarks>
    /// <para>
    /// If requestId is not found (e.g., timeout already occurred),
    /// the call is silently ignored. This is intentional to avoid
    /// race conditions between timeout and response.
    /// </para>
    /// <para>
    /// <b>Thread-safe:</b> Can be called from any thread.
    /// </para>
    /// </remarks>
    void SendResponse(string requestId, AgentEvent response);

    /// <summary>
    /// Waits for a response to a previously emitted request.
    /// Used for request/response patterns (permissions, clarifications).
    /// </summary>
    /// <typeparam name="T">Expected response event type</typeparam>
    /// <param name="requestId">Unique identifier matching the request event</param>
    /// <param name="timeout">Maximum time to wait for response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The typed response event</returns>
    /// <exception cref="TimeoutException">No response received within timeout</exception>
    /// <exception cref="OperationCanceledException">Operation was cancelled</exception>
    /// <exception cref="InvalidOperationException">Response type mismatch</exception>
    /// <remarks>
    /// <para>
    /// This method is used by middlewares that need bidirectional communication:
    /// </para>
    /// <list type="number">
    /// <item>Middleware emits request event (e.g., PermissionRequestEvent)</item>
    /// <item>Middleware calls WaitForResponseAsync() - BLOCKS HERE</item>
    /// <item>Handler receives request event (via agent's event loop)</item>
    /// <item>User provides input</item>
    /// <item>Handler calls SendResponse()</item>
    /// <item>Middleware receives response and continues</item>
    /// </list>
    /// <para>
    /// <b>Timeout vs. Cancellation:</b>
    /// - TimeoutException: No response received within the specified timeout
    /// - OperationCanceledException: External cancellation (e.g., user stopped agent)
    /// </para>
    /// </remarks>
    Task<T> WaitForResponseAsync<T>(
        string requestId,
        TimeSpan timeout,
        CancellationToken cancellationToken) where T : AgentEvent;
}
