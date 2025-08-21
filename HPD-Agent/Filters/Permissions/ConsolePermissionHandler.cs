using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Default console-based permission handler for command-line applications.
/// </summary>
public class ConsolePermissionHandler : IPermissionHandler
{
    public async Task<PermissionDecision> RequestFunctionPermissionAsync(FunctionPermissionRequest request)
    {
        // Offload the blocking Console.ReadLine to a background thread
        return await Task.Run(() =>
        {
            Console.WriteLine($"\n[PERMISSION REQUIRED]");
            Console.WriteLine($"Function: {request.FunctionName}");
            Console.WriteLine($"Description: {request.FunctionDescription}");

            if (request.Arguments.Any())
            {
                Console.WriteLine("Arguments:");
                foreach (var arg in request.Arguments)
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

    public async Task<ContinuationDecision> RequestContinuationPermissionAsync(ContinuationPermissionRequest request)
    {
        // Offload the blocking Console.ReadLine to a background thread
        return await Task.Run(() =>
        {
            Console.WriteLine($"\n[CONTINUATION PERMISSION]");
            Console.WriteLine($"Function calling has reached iteration {request.CurrentIteration} of {request.MaxIterations}.");

            if (request.PlannedFunctions.Any())
            {
                Console.WriteLine($"The agent plans to call: {string.Join(", ", request.PlannedFunctions)}");
            }

            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("  [C]ontinue");
            Console.WriteLine("  [S]top");
            Console.Write("Choice: ");

            var response = Console.ReadLine()?.ToUpper();

            var decision = new ContinuationDecision 
            { 
                ShouldContinue = response == "C",
                Reason = response == "S" ? "User chose to stop the operation." : null
            };

            return decision;
        });
    }
}
