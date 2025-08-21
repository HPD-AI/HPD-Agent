using System;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

/// <summary>
/// Debug filter to see if permission filters are running and what function metadata looks like
/// </summary>
public class DebugPermissionFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(
        AiFunctionContext context,
        Func<AiFunctionContext, Task> next)
    {
        var functionName = context.ToolCallRequest?.FunctionName ?? "<unknown>";
        
        Console.WriteLine($"[DEBUG] Function: {functionName}");
        Console.WriteLine($"[DEBUG] Function Type: {context.Function?.GetType().Name}");
        
        // Check if it's an HPDAIFunction
        if (context.Function is HPDAIFunctionFactory.HPDAIFunction hpdFunction)
        {
            Console.WriteLine($"[DEBUG] HPDAIFunction detected!");
            Console.WriteLine($"[DEBUG] RequiresPermission: {hpdFunction.HPDOptions.RequiresPermission}");
            Console.WriteLine($"[DEBUG] Function Description: {hpdFunction.Description}");
        }
        else
        {
            Console.WriteLine($"[DEBUG] NOT an HPDAIFunction - Type: {context.Function?.GetType().FullName}");
        }
        
        Console.WriteLine($"[DEBUG] ---- End Debug Info ----");

        // Continue to next filter
        await next(context);
    }
}