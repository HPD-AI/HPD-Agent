# Plugin Scoping Test Checklist (v2.0)

**Version**: 2.0 - Now includes MCP and Frontend tool scoping!

Run `dotnet run --project AgentConsoleTest` and check the debug output:

## Expected Output (with scoping disabled):

```
🔧 Registered tools:
 - CreateMemoryAsync
 - UpdateMemoryAsync
 - Add : Adds two numbers and returns the sum.
 - Multiply : Multiplies two numbers and returns the product.
 ... (all functions visible)
```

## Expected Output (with scoping enabled):

```
🔧 Registered tools:
 - CreateMemoryAsync [Always visible]
 - UpdateMemoryAsync [Always visible]
 - MathPlugin [CONTAINER] : Mathematical operations including addition, subtraction, multiplication, and more.
 - MCP_filesystem [CONTAINER] : MCP Server 'filesystem'. Contains 15 functions: ReadFile, WriteFile, ...
 - FrontendTools [CONTAINER] : Frontend UI tools for user interaction. Contains 10 functions: ...
```

After expanding MathPlugin:
```
🔧 Registered tools:
 - CreateMemoryAsync
 - UpdateMemoryAsync
 - Add [Plugin: MathPlugin] : Adds two numbers and returns the sum.
 - Multiply [Plugin: MathPlugin] : Multiplies two numbers and returns the product.
 - MCP_filesystem [CONTAINER]
 - FrontendTools [CONTAINER]
```

## If you DON'T see `[CONTAINER]` and `[Plugin: MathPlugin]`:

The source generator didn't detect the `[PluginScope]` attribute. Possible causes:
1. Attribute not imported properly
2. Source generator cache issue
3. Attribute definition missing

## If you DO see the metadata but agent still sees all functions:

The PluginScopingManager filtering isn't being applied. Check:
1. Is `GetToolsForAgentTurn()` being called?
2. Is `expandedPlugins` empty initially?
3. Add breakpoint in PluginScopingManager to verify

## Enable Scoping in Program.cs:

```csharp
PluginScoping = new PluginScopingConfig
{
    Enabled = true,              // C# plugins
    ScopeMCPTools = true,        // MCP tools by server
    ScopeFrontendTools = true,   // Frontend AGUI tools
    MaxFunctionNamesInDescription = 10
}
```

## Test the two-turn flow:

### Test 1: C# Plugin (MathPlugin)
```
You: What math functions do you have?
Expected: Should only see MathPlugin container, not individual functions

You: Use MathPlugin
Expected: Container expands, returns "MathPlugin expanded. Available functions: Add, Multiply, ..."

You: Calculate 5 + 3
Expected: Uses Add function, returns 8
```

### Test 2: MCP Tools (if configured)
```
You: What filesystem tools do you have?
Expected: Should see MCP_filesystem container

You: Use the filesystem tools
Expected: MCP_filesystem expands, returns "filesystem server expanded. Available functions: ReadFile, WriteFile, ..."

You: Read config.json
Expected: Uses ReadFile from filesystem server
```

### Test 3: Frontend Tools (if using AGUI)
```
You: What UI tools do you have?
Expected: Should see FrontendTools container

You: Use the frontend tools
Expected: FrontendTools expands, returns "Frontend tools expanded. Available functions: ConfirmAction, ShowNotification, ..."

You: Show me a confirmation dialog
Expected: Uses ConfirmAction from frontend tools
```

## Verify Metadata:

Check debug output shows correct source types:
- C# Plugin containers have `SourceType` not set (default)
- MCP containers have `["SourceType"] = "MCP"` and `["MCPServerName"] = "filesystem"`
- Frontend containers have `["SourceType"] = "Frontend"`
