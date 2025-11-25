using System;
using System.Reflection;
using System.Linq;

namespace AgentConsoleTest;

public class InspectSpecializedAgents
{
    public static void Main(string[] args)
    {
        var asm = Assembly.GetExecutingAssembly();

        Console.WriteLine("=== Inspecting SpecializedAgents Code Generation ===\n");

        // Find the SpecializedAgentsRegistration class (should be in global namespace)
        var registryTypeName = "SpecializedAgentsRegistration";
        var registryType = asm.GetType(registryTypeName);

        if (registryType != null)
        {
            Console.WriteLine($"✅ Found registry type: {registryType.FullName}");
            Console.WriteLine($"\nMethods in this type:");
            foreach (var method in registryType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Console.WriteLine($"  - {method.Name}");
                Console.WriteLine($"    Parameters: {string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
            }
        }
        else
        {
            Console.WriteLine($"❌ Registry type '{registryTypeName}' not found in assembly");
            Console.WriteLine("\nSearching for any types with 'Specialized' or 'Registration' in the name:");

            var allTypes = asm.GetTypes()
                .Where(t => !t.Name.StartsWith("<") &&
                           (t.Name.Contains("Specialized") || t.Name.Contains("Registration")))
                .ToList();

            if (allTypes.Any())
            {
                foreach (var t in allTypes)
                {
                    Console.WriteLine($"  - {t.FullName}");
                }
            }
            else
            {
                Console.WriteLine("  (none found)");
            }

            Console.WriteLine("\n📋 All types in assembly:");
            foreach (var t in asm.GetTypes()
                .Where(t => !t.Name.StartsWith("<"))
                .OrderBy(t => t.FullName))
            {
                Console.WriteLine($"  - {t.FullName}");
            }
        }
    }
}
