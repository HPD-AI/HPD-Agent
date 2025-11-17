using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using HPD.Agent;
using HPD.Agent.Conversation;

namespace HPD_Agent.FFI;


/// <summary>
/// Delegate for streaming callback from C# to Rust
/// </summary>
/// <param name="context">Context pointer passed back to Rust</param>
/// <param name="eventJsonPtr">Pointer to UTF-8 JSON string of the event, or null to signal end of stream</param>
public delegate void StreamCallback(IntPtr context, IntPtr eventJsonPtr);

/// <summary>
/// Matches the Rust RustFunctionInfo structure
/// </summary>
public class RustFunctionInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("wrapperFunctionName")]
    public string WrapperFunctionName { get; set; } = string.Empty;
    
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "{}";
    
    [JsonPropertyName("requiresPermission")]
    public bool RequiresPermission { get; set; }
    
    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; set; } = new();
    
    [JsonPropertyName("plugin_name")]
    public string PluginName { get; set; } = string.Empty;
}

/// <summary>
/// Static class containing all C# functions exported to Rust via FFI.
/// This serves as the main entry point for the Rust wrapper library.
/// </summary>
public static partial class NativeExports
{
    /// <summary>
    /// Test function to verify FFI communication between C# and Rust.
    /// Accepts a UTF-8 string from Rust and returns a response.
    /// </summary>
    /// <param name="messagePtr">Pointer to a UTF-8 encoded string from Rust</param>
    /// <returns>Pointer to a UTF-8 encoded response string allocated by C#</returns>
    [UnmanagedCallersOnly(EntryPoint = "ping")]
    public static IntPtr Ping(IntPtr messagePtr)
    {
        try
        {
            // Marshal the string from Rust
            string? message = Marshal.PtrToStringUTF8(messagePtr);
            string response = $"Pong: You sent '{message}'";

            // Convert to UTF-8 bytes and allocate unmanaged memory
            byte[] responseBytes = Encoding.UTF8.GetBytes(response + '\0'); // null-terminated
            IntPtr responsePtr = Marshal.AllocHGlobal(responseBytes.Length);
            Marshal.Copy(responseBytes, 0, responsePtr, responseBytes.Length);
            
            return responsePtr;
        }
        catch (Exception ex)
        {
            // In case of error, return a pointer to an error message
            string errorResponse = $"Error in Ping: {ex.Message}";
            byte[] errorBytes = Encoding.UTF8.GetBytes(errorResponse + '\0'); // null-terminated
            IntPtr errorPtr = Marshal.AllocHGlobal(errorBytes.Length);
            Marshal.Copy(errorBytes, 0, errorPtr, errorBytes.Length);
            return errorPtr;
        }
    }

    /// <summary>
    /// Frees memory allocated by C# for strings returned to Rust.
    /// This must be called by Rust for every string pointer received from C#.
    /// </summary>
    /// <param name="stringPtr">Pointer to the string memory to free</param>
    [UnmanagedCallersOnly(EntryPoint = "free_string")]
    public static void FreeString(IntPtr stringPtr)
    {
        if (stringPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(stringPtr);
        }
    }

    /// <summary>
    /// Creates an agent with the given configuration and plugins.
    /// </summary>
    /// <param name="configJsonPtr">Pointer to JSON string containing AgentConfig</param>
    /// <param name="pluginsJsonPtr">Pointer to JSON string containing plugin definitions</param>
    /// <returns>Handle to the created Agent, or IntPtr.Zero on failure</returns>
    [UnmanagedCallersOnly(EntryPoint = "create_agent_with_plugins")]
    [RequiresUnreferencedCode("Agent creation uses plugin registration methods that require reflection.")]
    public static IntPtr CreateAgentWithPlugins(IntPtr configJsonPtr, IntPtr pluginsJsonPtr)
    {
        try
        {
            string? configJson = Marshal.PtrToStringUTF8(configJsonPtr);
            if (string.IsNullOrEmpty(configJson)) return IntPtr.Zero;

            var agentConfig = JsonSerializer.Deserialize<AgentConfig>(configJson, HPDFFIJsonContext.Default.AgentConfig);
            if (agentConfig == null) return IntPtr.Zero;

            var builder = new AgentBuilder(agentConfig);
            
            // Parse and add Rust plugins
            string? pluginsJson = Marshal.PtrToStringUTF8(pluginsJsonPtr);
            Console.WriteLine($"[FFI] Received plugins JSON: {pluginsJson}");
            
            if (!string.IsNullOrEmpty(pluginsJson))
            {
                try
                {
                    var rustFunctions = JsonSerializer.Deserialize(pluginsJson, HPDFFIJsonContext.Default.ListRustFunctionInfo);
                    Console.WriteLine($"[FFI] Deserialized {rustFunctions?.Count ?? 0} Rust functions");
                    
                    if (rustFunctions != null && rustFunctions.Count > 0)
                    {
                        // Track unique plugin names
                        var pluginNames = new HashSet<string>();
                        
                        foreach (var rustFunc in rustFunctions)
                        {
                            Console.WriteLine($"[FFI] Adding Rust function: {rustFunc.Name} - {rustFunc.Description}");
                            var aiFunction = CreateRustFunctionWrapper(rustFunc);
                            builder.AddRustFunction(aiFunction);
                            
                            // Track plugin name for registration
                            if (!string.IsNullOrEmpty(rustFunc.PluginName))
                            {
                                pluginNames.Add(rustFunc.PluginName);
                            }
                        }
                        
                        // Register plugin executors on Rust side
                        foreach (var pluginName in pluginNames)
                        {
                            Console.WriteLine($"[FFI] Registering executors for plugin: {pluginName}");
                            bool success = RustPluginFFI.RegisterPluginExecutors(pluginName);
                            Console.WriteLine($"[FFI] Registration result for {pluginName}: {success}");
                        }
                        
                        Console.WriteLine($"[FFI] Successfully added {rustFunctions.Count} Rust functions to agent");
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - agent can still work without Rust functions
                    Console.WriteLine($"Failed to parse Rust plugins: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }

            var agent = builder.BuildCoreAgent();
            return ObjectManager.Add(agent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create agent: {ex.Message}");
            return IntPtr.Zero;
        }
    }


    /// <summary>
    /// Creates an AIFunction wrapper that calls back to Rust via FFI
    /// </summary>
    private static AIFunction CreateRustFunctionWrapper(RustFunctionInfo rustFunc)
    {
        return HPDAIFunctionFactory.Create(
            (arguments, cancellationToken) =>
            {
                // Convert AIFunctionArguments to a simple dictionary
                var argsDict = new Dictionary<string, object>();
                foreach (var kvp in arguments)
                {
                    if (kvp.Key != "__raw_json__" && kvp.Value != null) // Skip internal keys and null values
                    {
                        argsDict[kvp.Key] = kvp.Value;
                    }
                }
                
                // Execute the Rust function via FFI
                var result = RustPluginFFI.ExecuteFunction(rustFunc.Name, argsDict);
                
                if (!result.Success)
                {
                    // Return error as structured response for better AI understanding
                    return Task.FromResult<object?>(new { error = result.Error ?? "Unknown error", success = false });
                }
                
                // Parse the result
                if (result.Result != null)
                {
                    try
                    {
                        using (result.Result)
                        {
                            var root = result.Result.RootElement;
                            
                            // Check if it's a success/result envelope
                            if (root.TryGetProperty("success", out var successProp) && 
                                root.TryGetProperty("result", out var resultProp))
                            {
                                if (successProp.GetBoolean())
                                {
                                    // Return just the result value
                                    return Task.FromResult<object?>(resultProp.ValueKind == JsonValueKind.String 
                                        ? resultProp.GetString() 
                                        : resultProp.GetRawText());
                                }
                                else if (root.TryGetProperty("error", out var errorProp))
                                {
                                    return Task.FromResult<object?>(new { error = errorProp.GetString(), success = false });
                                }
                            }
                            
                            // Return raw response if not in envelope format
                            return Task.FromResult<object?>(root.GetRawText());
                        }
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult<object?>(new { error = $"Failed to parse result: {ex.Message}", success = false });
                    }
                }
                
                return Task.FromResult<object?>(null);
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = rustFunc.Name,
                Description = rustFunc.Description,
                RequiresPermission = rustFunc.RequiresPermission,
                SchemaProvider = () => 
                {
                    try
                    {
                        // Parse the schema JSON from Rust
                        var schemaDoc = JsonDocument.Parse(rustFunc.Schema);
                        var rootSchema = schemaDoc.RootElement;
                        
                        // Check if this is an OpenAPI function calling format
                        if (rootSchema.TryGetProperty("function", out var functionElement) &&
                            functionElement.TryGetProperty("parameters", out var parametersElement))
                        {
                            // Extract just the parameters schema for Microsoft.Extensions.AI
                            return parametersElement.Clone();
                        }
                        else
                        {
                            // Use the schema as-is if it's already in the right format
                            return rootSchema.Clone();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error and fallback to empty object schema
                        Console.WriteLine($"Warning: Failed to parse schema for {rustFunc.Name}: {ex.Message}");
                        return JsonDocument.Parse("{}").RootElement;
                    }
                }
            }
        );
    }

    /// <summary>
    /// Destroys an agent and releases its resources.
    /// </summary>
    /// <param name="agentHandle">Handle to the agent to destroy</param>
    [UnmanagedCallersOnly(EntryPoint = "destroy_agent")]
    public static void DestroyAgent(IntPtr agentHandle)
    {
        ObjectManager.Remove(agentHandle);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Future APIs:
    // - Direct Agent streaming API (using ConversationThread for state)
    // - Protocol adapter exports (Microsoft.Agents.AI, AGUI)
    // - Checkpoint/resume support via ConversationThread serialization
    // ════════════════════════════════════════════════════════════════════════════
}
