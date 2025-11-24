// Simple test to verify middleware registration fix
using HPD.Agent;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

Console.WriteLine("🧪 Testing Middleware Registration Fix...\n");

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("❌ OPENAI_API_KEY not set. Using dummy key for structural test.");
    apiKey = "sk-dummy-key-for-structure-test";
}

try
{
    var agent = new AgentBuilder()
        .WithProvider("openai", "gpt-4o-mini", apiKey)
        .WithLogging(loggerFactory, includeFunctionInvocations: true)
        .BuildCoreAgent();

    Console.WriteLine("✅ Agent created successfully!");

    // Check if middlewares are registered
    var scopedManager = agent.ScopedFunctionMiddlewareManager;
    if (scopedManager == null)
    {
        Console.WriteLine("❌ ScopedFunctionMiddlewareManager is null!");
        return 1;
    }

    var globalMiddlewares = scopedManager.GetGlobalMiddlewares();
    Console.WriteLine($"\n📊 Global middlewares registered: {globalMiddlewares.Count}");

    if (globalMiddlewares.Count == 0)
    {
        Console.WriteLine("❌ NO global middlewares registered! The fix didn't work.");
        return 1;
    }

    foreach (var middleware in globalMiddlewares)
    {
        Console.WriteLine($"   ✓ {middleware.GetType().Name}");
    }

    // Check if LoggingAIFunctionMiddleware is present
    bool hasLoggingMiddleware = globalMiddlewares.Any(m =>
        m.GetType().Name.Contains("Logging"));

    if (hasLoggingMiddleware)
    {
        Console.WriteLine("\n✅ SUCCESS! LoggingAIFunctionMiddleware is registered!");
        Console.WriteLine("✅ The middleware fix is working correctly!");
        return 0;
    }
    else
    {
        Console.WriteLine("\n❌ FAILED! LoggingAIFunctionMiddleware was not found!");
        return 1;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test FAILED with exception: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}
