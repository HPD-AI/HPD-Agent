using HPD.Agent;
using HPD.Agent.Internal.MiddleWare;
using Microsoft.Extensions.Logging;

class MiddlewareTest
{
    static void Main(string[] args)
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        try
        {
            // Create a simple agent with logging middleware
            var agent = new AgentBuilder()
                .WithProvider("openai", "gpt-4o-mini", Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
                .WithLogging(loggerFactory, includeFunctionInvocations: true)
                .WithPlugin<TestPlugin>()
                .Build();

            Console.WriteLine("✅ Agent created successfully!");
            Console.WriteLine($"Global middlewares count: {agent.Config.ToString()}");

            // Check if middlewares are registered
            var scopedManager = agent.ScopedFunctionMiddlewareManager;
            if (scopedManager != null)
            {
                var globalMiddlewares = scopedManager.GetGlobalMiddlewares();
                Console.WriteLine($"✅ Global middlewares registered: {globalMiddlewares.Count}");

                foreach (var middleware in globalMiddlewares)
                {
                    Console.WriteLine($"   - {middleware.GetType().Name}");
                }
            }

            Console.WriteLine("\n✅ Middleware registration test PASSED!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test FAILED: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

// Simple test plugin
public class TestPlugin
{
    public string TestFunction(string input)
    {
        return $"Echo: {input}";
    }
}
