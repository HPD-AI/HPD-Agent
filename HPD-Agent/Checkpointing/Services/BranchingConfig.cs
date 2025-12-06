namespace HPD.Agent.Checkpointing.Services;

/// <summary>
/// Configuration for the BranchingService.
/// Controls whether branching features are available.
/// </summary>
public class BranchingConfig
{
    /// <summary>
    /// Whether branching is enabled.
    /// When false, all branching operations throw InvalidOperationException.
    /// </summary>
    public bool Enabled { get; set; }
}
