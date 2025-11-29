using System.Collections.Immutable;

namespace HPD.Agent;

/// <summary>
/// State for total error threshold tracking. Immutable record with static abstract key.
/// Tracks the total number of errors encountered across all iterations (regardless of type).
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
/// var thresholdState = context.State.GetState&lt;TotalErrorThresholdState&gt;();
/// var totalErrors = thresholdState.TotalErrorCount;
///
/// // Update state
/// context.UpdateState&lt;TotalErrorThresholdState&gt;(s => s with
/// {
///     TotalErrorCount = s.TotalErrorCount + newErrorsThisIteration
/// });
/// </code>
///
/// <para><b>Difference from ErrorTrackingState:</b></para>
/// <list type="table">
/// <listheader>
///   <term>Aspect</term>
///   <description>TotalErrorThresholdState</description>
/// </listheader>
/// <item>
///   <term>What it counts</term>
///   <description>ALL errors, regardless of type or consecutiveness</description>
/// </item>
/// <item>
///   <term>Resets after</term>
///   <description>Never - accumulates over entire agent run</description>
/// </item>
/// <item>
///   <term>Use case</term>
///   <description>Prevent total degradation from mixed error scenarios</description>
/// </item>
/// </list>
/// </remarks>
public sealed record TotalErrorThresholdState : IMiddlewareState
{
    /// <summary>
    /// Unique key for this middleware state type.
    /// </summary>
    public static string Key => "HPD.Agent.TotalErrorThreshold";

    /// <summary>
    /// Creates default/initial state with zero error count.
    /// </summary>
    public static IMiddlewareState CreateDefault() => new TotalErrorThresholdState();

    /// <summary>
    /// Total number of errors encountered in this agent run.
    /// Cumulative - never resets (unlike ErrorTrackingState which resets on success).
    /// </summary>
    public int TotalErrorCount { get; init; } = 0;
}
