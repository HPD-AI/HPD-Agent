namespace HPD.Agent;

/// <summary>
/// State for continuation permission tracking. Immutable record with static abstract key.
/// Tracks the current extended iteration limit that can be increased when user approves continuation.
/// </summary>
/// <remarks>
/// <para><b>Thread Safety:</b></para>
/// <para>
/// This state is immutable and flows through the context.
/// It is NOT stored in middleware instance fields, preserving thread safety
/// for concurrent RunAsync() calls.
/// </para>
///
/// <para><b>Usage:</b></para>
/// <code>
/// // Read state
/// var permState = context.State.GetState&lt;ContinuationPermissionState&gt;();
/// if (context.Iteration > permState.CurrentExtendedLimit - 1) { ... }
///
/// // Extend limit when user approves
/// context.UpdateState&lt;ContinuationPermissionState&gt;(s => s.ExtendLimit(extensionAmount));
/// </code>
/// </remarks>
public sealed record ContinuationPermissionState : IMiddlewareState
{
    /// <summary>
    /// Unique key for this middleware state type.
    /// </summary>
    public static string Key => "HPD.Agent.ContinuationPermission";

    /// <summary>
    /// Creates default/initial state.
    /// Note: The initial limit should be set by the middleware during initialization.
    /// </summary>
    public static IMiddlewareState CreateDefault() => new ContinuationPermissionState();

    /// <summary>
    /// The current extended iteration limit.
    /// This starts at the configured maxIterations and increases when user approves continuation.
    /// </summary>
    public int CurrentExtendedLimit { get; init; } = 20; // Default matches middleware default

    /// <summary>
    /// Creates state with a specific initial limit.
    /// </summary>
    /// <param name="initialLimit">The initial iteration limit</param>
    /// <returns>New state with the specified limit</returns>
    public static ContinuationPermissionState WithInitialLimit(int initialLimit) =>
        new() { CurrentExtendedLimit = initialLimit };

    /// <summary>
    /// Extends the iteration limit by the specified amount.
    /// Called when user approves continuation.
    /// </summary>
    /// <param name="extensionAmount">Number of iterations to add</param>
    /// <returns>New state with extended limit</returns>
    public ContinuationPermissionState ExtendLimit(int extensionAmount) =>
        this with { CurrentExtendedLimit = CurrentExtendedLimit + extensionAmount };
}
