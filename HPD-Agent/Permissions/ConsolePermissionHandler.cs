using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Console-based permission handler for continuation permissions only.
/// Function-level permissions are now handled by ConsolePermissionFilter.
/// </summary>
public class ConsolePermissionHandler : IPermissionHandler
{

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
