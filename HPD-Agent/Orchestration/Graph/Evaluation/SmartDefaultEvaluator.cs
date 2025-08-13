/// <summary>
/// Smart default condition evaluator that handles common cases automatically.
/// Extensible for complex scenarios.
/// </summary>
public class SmartDefaultEvaluator<TState> : IConditionEvaluator<TState> where TState : class, new()
{
    public virtual Task<bool> EvaluateAsync(string condition, WorkflowContext<TState> context, CancellationToken cancellationToken)
    {
        var lower = condition.Trim().ToLowerInvariant();
        var result = lower switch
        {
            "" or "true" or "continue" => true,
            "false" or "stop" or "end" => false,
            _ => EvaluateCustomCondition(lower, context)
        };
        return Task.FromResult(result);
    }

    /// <summary>
    /// Override this method for custom condition logic beyond built-in cases.
    /// </summary>
    protected virtual bool EvaluateCustomCondition(string condition, WorkflowContext<TState> context)
    {
        // Default to true (fail-open)
        return true;
    }
}
