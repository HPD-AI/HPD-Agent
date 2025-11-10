using System.Threading.Tasks;
using HPD.Agent.Internal.Filters;

namespace HPD.Agent.Internal.Filters;

/// <summary>
/// Internal interface for permission filters that can approve or deny function executions.
/// Implements IAiFunctionFilter to integrate naturally with the filter pipeline.
/// NOT exposed to users - implementation detail for HPD-Agent internals.
/// </summary>
internal interface IPermissionFilter : IAiFunctionFilter
{
    // Inherits Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    // Permission-specific methods can be added later if needed
}