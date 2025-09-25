using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Console-based permission filter for command-line applications.
/// Directly implements permission checking with optional storage.
/// </summary>
public class ConsolePermissionFilter : IPermissionFilter
{
    private readonly IPermissionStorage? _storage;

    public ConsolePermissionFilter(IPermissionStorage? storage = null)
    {
        _storage = storage;
    }

    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        // Check if function requires permission
        if (context.Function is not HPDAIFunctionFactory.HPDAIFunction hpdFunction ||
            !hpdFunction.HPDOptions.RequiresPermission)
        {
            await next(context);
            return;
        }

        var functionName = context.ToolCallRequest.FunctionName;
        var conversationId = context.Conversation.Id;
        context.Conversation.Metadata.TryGetValue("Project", out var projectObj);
        var projectId = (projectObj as Project)?.Id;

        // Check storage if available
        if (_storage != null)
        {
            var storedChoice = await _storage.GetStoredPermissionAsync(functionName, conversationId, projectId);

            if (storedChoice == PermissionChoice.AlwaysAllow)
            {
                await next(context);
                return;
            }

            if (storedChoice == PermissionChoice.AlwaysDeny)
            {
                context.Result = $"Execution of '{functionName}' was denied by a stored user preference.";
                context.IsTerminated = true;
                return;
            }
        }

        // No stored preference, request permission via console
        var decision = await RequestPermissionAsync(functionName, context.Function.Description, context.ToolCallRequest.Arguments);

        // Store decision if user chose to remember
        if (_storage != null && decision.Storage != null)
        {
            await _storage.SavePermissionAsync(
                functionName,
                decision.Storage.Choice,
                decision.Storage.Scope,
                conversationId,
                projectId);
        }

        // Apply decision
        if (decision.Approved)
        {
            await next(context);
        }
        else
        {
            context.Result = $"Execution of '{functionName}' was denied by the user.";
            context.IsTerminated = true;
        }
    }

    private async Task<PermissionDecision> RequestPermissionAsync(string functionName, string functionDescription, System.Collections.Generic.IDictionary<string, object?> arguments)
    {
        // Offload the blocking Console.ReadLine to a background thread
        return await Task.Run(() =>
        {
            Console.WriteLine($"\n[PERMISSION REQUIRED]");
            Console.WriteLine($"Function: {functionName}");
            Console.WriteLine($"Description: {functionDescription}");

            if (arguments.Any())
            {
                Console.WriteLine("Arguments:");
                foreach (var arg in arguments)
                {
                    Console.WriteLine($"  {arg.Key}: {arg.Value}");
                }
            }

            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("  [A]llow once");
            Console.WriteLine("  [D]eny once");
            Console.WriteLine("  [Y] Always allow (Global)");
            Console.WriteLine("  [N] Never allow (Global)");
            Console.Write("Choice: ");

            var response = Console.ReadLine()?.ToUpper();

            var decision = response switch
            {
                "A" => new PermissionDecision { Approved = true },
                "D" => new PermissionDecision { Approved = false },
                "Y" => new PermissionDecision
                {
                    Approved = true,
                    Storage = new PermissionStorage { Choice = PermissionChoice.AlwaysAllow, Scope = PermissionScope.Global }
                },
                "N" => new PermissionDecision
                {
                    Approved = false,
                    Storage = new PermissionStorage { Choice = PermissionChoice.AlwaysDeny, Scope = PermissionScope.Global }
                },
                _ => new PermissionDecision { Approved = false } // Default to deny
            };

            return decision;
        });
    }
}