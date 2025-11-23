using System;
using HPD.Agent.Internal.MiddleWare;
using HPD.Agent;
using System.Threading.Tasks;

/// <summary>
/// Auto-approve permission Middleware for testing and automation scenarios.
/// Automatically approves all function executions that require permission.
/// </summary>
internal class AutoApprovePermissionMiddleware : IPermissionMiddleware
{
    public async Task InvokeAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // Check if function requires permission
        if (context.Function is not HPDAIFunctionFactory.HPDAIFunction hpdFunction ||
            !hpdFunction.HPDOptions.RequiresPermission)
        {
            await next(context);
            return;
        }

        // Auto-approve all permission requests
        await next(context);
    }
}