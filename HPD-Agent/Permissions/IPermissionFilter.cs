using System.Threading.Tasks;
using HPD.Agent.Internal.MiddleWare;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Internal interface for permission Middlewares that can approve or deny function executions.
/// Implements IAIFunctionMiddleware to integrate naturally with the Middleware pipeline.
/// NOT exposed to users - implementation detail for HPD-Agent internals.
/// </summary>
internal interface IPermissionMiddleware : IAIFunctionMiddleware
{
    // Inherits Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    // Permission-specific methods can be added later if needed
}