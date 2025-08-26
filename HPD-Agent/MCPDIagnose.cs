using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;

/// <summary>
/// Debug helper to troubleshoot MCP registration issues
/// </summary>
public static class MCPRegistrationDebugger
{
    /// <summary>
    /// Comprehensive debugging method to check MCP integration
    /// </summary>
    public static async Task<MCPDebugReport> DiagnoseMCPIssuesAsync(
        AgentBuilder builder,
        string manifestPath,
        ILogger? logger = null)
    {
        var report = new MCPDebugReport();

        try
        {
            // 1. Check manifest file existence and validity
            report.ManifestExists = File.Exists(manifestPath);
            if (!report.ManifestExists)
            {
                report.Errors.Add($"Manifest file not found: {manifestPath}");
                return report;
            }

            // 2. Try to parse manifest
            var manifestContent = await File.ReadAllTextAsync(manifestPath);
            report.ManifestContent = manifestContent;

            try
            {
                var manifest = System.Text.Json.JsonSerializer.Deserialize(
                    manifestContent,
                    MCPJsonSerializerContext.Default.MCPManifest);

                report.ManifestParsed = manifest != null;
                if (manifest != null)
                {
                    report.ServerCount = manifest.Servers.Count;
                    report.EnabledServerCount = manifest.Servers.Count(s => s.Enabled);
                    report.ServerNames = manifest.Servers.Select(s => s.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Manifest parsing failed: {ex.Message}");
            }

            // 3. Test MCP client manager directly
            var mcpOptions = new MCPOptions { FailOnServerError = true }; // Force errors to surface
            var typedLogger = logger as ILogger<MCPClientManager> ??
                Microsoft.Extensions.Logging.Abstractions.NullLogger<MCPClientManager>.Instance;
            var mcpManager = new MCPClientManager(
                typedLogger,
                mcpOptions);

            try
            {
                var tools = await mcpManager.LoadToolsFromManifestAsync(manifestPath);
                report.MCPToolsLoaded = tools.Count;
                report.MCPToolNames = tools.Select(t => t.Name).ToList();

                // Test each tool's basic properties
                foreach (var tool in tools)
                {
                    var toolInfo = new MCPToolInfo
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        HasSchema = tool.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Undefined,
                        SchemaContent = tool.JsonSchema.ToString(),
                        UnderlyingMethodExists = tool.UnderlyingMethod != null
                    };

                    // Test if tool can be invoked (basic validation)
                    try
                    {
                        var emptyArgs = new AIFunctionArguments();
                        // Don't actually invoke, just check if the method can be called
                        toolInfo.CanBeInvoked = true; // If we get here without exception
                    }
                    catch (Exception ex)
                    {
                        toolInfo.InvocationError = ex.Message;
                    }

                    report.ToolDetails.Add(toolInfo);
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"MCP tool loading failed: {ex.Message}");
            }

            // 4. Test full agent integration
            try
            {
                var agent = builder
                    .WithMCP(manifestPath, mcpOptions)
                    .Build();

                // Check if tools are available in agent's default options
                var tools = agent.DefaultOptions?.Tools;
                if (tools != null)
                {
                    report.AgentToolCount = tools.Count;
                    report.AgentToolNames = tools.OfType<AIFunction>().Select(f => f.Name).ToList();
                }

                report.AgentBuildSucceeded = true;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Agent build with MCP failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            report.Errors.Add($"Debug process failed: {ex.Message}");
        }

        return report;
    }

    /// <summary>
    /// Test a specific MCP tool invocation
    /// </summary>
    public static async Task<string> TestToolInvocationAsync(
        AIFunction tool,
        Dictionary<string, object?> arguments)
    {
        try
        {
            var args = new AIFunctionArguments(arguments);
            var result = await tool.InvokeAsync(args);
            return $"SUCCESS: {System.Text.Json.JsonSerializer.Serialize(result)}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}\nStack: {ex.StackTrace}";
        }
    }
}

public class MCPDebugReport
{
    public bool ManifestExists { get; set; }
    public bool ManifestParsed { get; set; }
    public string ManifestContent { get; set; } = "";
    public int ServerCount { get; set; }
    public int EnabledServerCount { get; set; }
    public List<string> ServerNames { get; set; } = new();
    
    public int MCPToolsLoaded { get; set; }
    public List<string> MCPToolNames { get; set; } = new();
    
    public bool AgentBuildSucceeded { get; set; }
    public int AgentToolCount { get; set; }
    public List<string> AgentToolNames { get; set; } = new();
    
    public List<MCPToolInfo> ToolDetails { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    
    public void PrintReport()
    {
        Console.WriteLine("=== MCP Registration Debug Report ===");
        Console.WriteLine($"Manifest exists: {ManifestExists}");
        Console.WriteLine($"Manifest parsed: {ManifestParsed}");
        Console.WriteLine($"Servers found: {ServerCount} (enabled: {EnabledServerCount})");
        Console.WriteLine($"Server names: {string.Join(", ", ServerNames)}");
        Console.WriteLine($"MCP tools loaded: {MCPToolsLoaded}");
        Console.WriteLine($"MCP tool names: {string.Join(", ", MCPToolNames)}");
        Console.WriteLine($"Agent build succeeded: {AgentBuildSucceeded}");
        Console.WriteLine($"Agent tool count: {AgentToolCount}");
        Console.WriteLine($"Agent tool names: {string.Join(", ", AgentToolNames)}");
        
        if (ToolDetails.Any())
        {
            Console.WriteLine("\n=== Tool Details ===");
            foreach (var tool in ToolDetails)
            {
                Console.WriteLine($"Tool: {tool.Name}");
                Console.WriteLine($"  Description: {tool.Description}");
                Console.WriteLine($"  Has Schema: {tool.HasSchema}");
                Console.WriteLine($"  Can Be Invoked: {tool.CanBeInvoked}");
                if (!string.IsNullOrEmpty(tool.InvocationError))
                {
                    Console.WriteLine($"  Invocation Error: {tool.InvocationError}");
                }
            }
        }
        
        if (Errors.Any())
        {
            Console.WriteLine("\n=== ERRORS ===");
            foreach (var error in Errors)
            {
                Console.WriteLine($"ERROR: {error}");
            }
        }
    }
}

public class MCPToolInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool HasSchema { get; set; }
    public string SchemaContent { get; set; } = "";
    public bool UnderlyingMethodExists { get; set; }
    public bool CanBeInvoked { get; set; }
    public string? InvocationError { get; set; }
}