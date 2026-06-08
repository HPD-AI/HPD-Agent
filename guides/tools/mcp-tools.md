# MCP Tools

MCP servers are external tool sources. HPD Agent connects to configured MCP servers, imports their tools, and exposes them as model-callable functions.

## Configure MCP From JSON Content

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

var manifest = """
{
  "servers": [
    {
      "name": "local-stdio",
      "command": "dotnet",
      "arguments": ["--info"],
      "enabled": false,
      "requiresPermission": false,
      "enablecollapsing": true
    }
  ]
}
""";

var builder = new AgentBuilder()
    .WithMCPContent(manifest, new MCPOptions
    {
        FailOnServerError = true,
        ConnectionTimeout = TimeSpan.FromSeconds(5)
    });
```

This shape configures MCP without relying on an external paid service. A disabled server is useful for validating configuration shape without starting a process.

## Stdio Servers

For stdio servers, configure:

- `name`
- `command`
- `arguments`
- `enabled`
- `requiresPermission`

The current stdio startup path uses the configured command and arguments.

## Harness-Owned MCP Servers

A generated harness can contribute MCP server config:

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

public sealed class ExternalTools
{
    [MCPServer(Description = "Local MCP server", CollapseWithinToolHarness = true)]
    public MCPServerConfig LocalMcp() => new()
    {
        Name = "local-stdio",
        Command = "dotnet",
        Arguments = ["--info"],
        Enabled = false,
        RequiresPermission = false,
    };
}
```

The generated path registers an MCP server source for the harness. It does not turn the method itself into a model-facing function.

## Boundary

`MCPServerConfig` contains process-isolation configuration, but the current stdio startup path does not enforce that config when creating the MCP process. Do not rely on MCP `processIsolation` for runtime sandboxing. Use ordinary permissions and local sandboxing around agent tools that need process control.
