/// <summary>
/// Defines conditional evaluation logic for workflow transitions.
/// </summary>
public interface IConditionEvaluator<TState> where TState : class, new()
{
    /// <summary>
    /// Returns true if the given condition string holds in the current context.
    /// </summary>
    Task<bool> EvaluateAsync(string condition, WorkflowContext<TState> context, CancellationToken cancellationToken);
}
