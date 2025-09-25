using System.Threading.Tasks;

/// <summary>
/// Interface for permission filters that can approve or deny function executions.
/// Implements IAiFunctionFilter to integrate naturally with the filter pipeline.
/// </summary>
public interface IPermissionFilter : IAiFunctionFilter
{
    // Inherits Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    // Permission-specific methods can be added later if needed
}