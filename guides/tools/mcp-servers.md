# MCP Servers

MCP servers are external tool sources. HPD Agent connects to configured MCP servers, imports their tools, and exposes them as model-callable functions. Servers can run locally over stdio or remotely over HTTP.

## Quick Start: Stdio

Create an MCP manifest and register it with the agent:

```json
{
  "servers": [
    {
      "name": "filesystem",
      "transport": "stdio",
      "command": "npx",
      "arguments": ["-y", "@modelcontextprotocol/server-filesystem", "."],
      "workingDirectory": "/workspace",
      "enabled": true
    }
  ]
}
```

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

var agent = await new AgentBuilder()
    .WithMCP("mcp.json")
    .BuildAsync();
```

Every server entry must declare `transport`. For local servers, use `transport: "stdio"` with `command` and optional `arguments`.

## Remote HTTP

Use `transport: "http"` for remote MCP servers:

```json
{
  "servers": [
    {
      "name": "search",
      "transport": "http",
      "endpoint": "https://mcp.example.com/mcp",
      "httpTransportMode": "auto",
      "headers": {
        "X-Workspace": "example"
      },
      "headerSecretKeys": {
        "Authorization": "mcp:search:Authorization"
      }
    }
  ]
}
```

Use literal `headers` for non-secret values and `headerSecretKeys` for values resolved by HPD's secret resolver.

## JSON Content

You can also pass manifest JSON directly:

```csharp
var manifest = """
{
  "servers": [
    {
      "name": "local-stdio",
      "transport": "stdio",
      "command": "dotnet",
      "arguments": ["--info"],
      "enabled": false
    }
  ]
}
""";

var agent = await new AgentBuilder()
    .WithMCPContent(manifest, new MCPOptions
    {
        FailOnServerError = true
    })
    .BuildAsync();
```

A disabled server is useful for validating configuration shape without starting the process.

## OAuth

OAuth server/client configuration belongs in the manifest. Browser login, token cache, and dynamic registration persistence belong to application code through `MCPOptions.OAuthRuntime`.

```json
{
  "servers": [
    {
      "name": "enterprise",
      "transport": "http",
      "endpoint": "https://mcp.example.com/mcp",
      "oauth": {
        "redirectUri": "http://localhost:8787/callback",
        "clientId": "client-id",
        "clientSecretKey": "mcp:enterprise:ClientSecret",
        "scopes": ["read", "write"]
      }
    }
  ]
}
```

```csharp
var agent = await new AgentBuilder()
    .WithMCP("mcp.json", options =>
    {
        options.OAuthRuntime = new JsonMcpOAuthRuntime(
            "~/.hpd/mcp/oauth",
            authorizationRedirectDelegate: McpOAuthRedirectHandlers.LocalBrowser());
    })
    .BuildAsync();
```

Built-in runtimes:

- `InMemoryMcpOAuthRuntime` stores tokens and dynamic client registrations for the runtime instance.
- `JsonMcpOAuthRuntime` stores tokens and dynamic client registrations as JSON files.

## Stdio Process Isolation

Stdio MCP servers are external processes. Use `processIsolation` when a local server should run inside HPD's process boundary instead of launching directly through the MCP SDK's default stdio transport.

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem", "."],
  "processIsolation": {
    "mode": "Isolated",
    "profile": "filesystem-only",
    "allowWrite": ["."],
    "denyRead": ["~/.ssh", "~/.aws", "~/.gnupg"],
    "networkMode": "Blocked"
  }
}
```

The host application must provide an MCP process provider to enforce this policy:

```csharp
var agent = await new AgentBuilder()
    .WithMCP("mcp.json", options =>
    {
        options.ProcessProvider = myProcessProvider;
    })
    .BuildAsync();
```

If `processIsolation.mode` is `Isolated` and no provider is configured, HPD fails closed instead of launching the MCP server unsandboxed. HTTP MCP servers do not use `processIsolation` because HPD does not launch their server process.

`processIsolation` currently has 20 manifest fields, grouped into seven policy areas:

| Area | Fields |
| --- | --- |
| Mode/profile | `mode`, `profile` |
| Filesystem | `allowRead`, `denyRead`, `allowWrite`, `denyWrite` |
| Network | `networkMode`, `allowedDomains`, `deniedDomains` |
| Unix sockets | `allowUnixSockets`, `allowAllUnixSockets` |
| Environment | `allowedEnvironmentVariables`, `stripUnlistedEnvironmentVariables` |
| TLS trust | `tlsTrustMode`, `injectTlsTrustEnvironmentVariables` |
| Process behavior | `allowPty`, `allowLocalBinding`, `allowedMachLookups`, `ignoreViolations`, `violationAction` |

Named profiles are `filesystem-only`, `network-only`, `permissive`, and `disabled`. Omitting `profile` uses HPD's restrictive default profile.

## Resources And Prompts

MCP resources and prompts are not imported automatically as agent context. Enable them when you want generic MCP functions for listing and reading them.

```json
{
  "name": "workspace",
  "transport": "stdio",
  "command": "mcp-server",
  "enableResources": true,
  "maxResourceListResults": 100,
  "maxResourceContentLength": 200000,
  "enablePrompts": true,
  "maxPromptListResults": 100,
  "maxPromptContentLength": 200000
}
```

Resource functions:

- `mcp_{server}_list_resources`
- `mcp_{server}_list_resource_templates`
- `mcp_{server}_read_resource`

Prompt functions:

- `mcp_{server}_list_prompts`
- `mcp_{server}_get_prompt`

Text reads are capped by the configured content limits. Binary resource content returns metadata instead of base64 payloads.

## Live Updates

Live updates emit HPD agent events when an MCP server reports that tools, prompts, resources, or a subscribed resource URI changed.

```json
{
  "name": "workspace",
  "transport": "stdio",
  "command": "mcp-server",
  "enableLiveUpdates": true,
  "resourceSubscriptions": [
    "file:///workspace/README.md"
  ]
}
```

```csharp
using var subscription = agent.Subscribe<McpServerChangedEvent>(evt =>
{
    Console.WriteLine($"{evt.ServerName}: {evt.ChangeKind} changed");
});
```

Live update event types:

- `McpServerChangedEvent`
- `McpLiveUpdatesStartedEvent`
- `McpLiveUpdatesStoppedEvent`
- `McpLiveUpdatesErrorEvent`

Live updates are invalidation signals. HPD does not mutate the active tool list mid-turn. Restarting or rebuilding the agent picks up the updated server tool list.

## Collapsing And Server Instructions

Set `enablecollapsing` to group all functions from a server behind an `MCP_{server}` container.

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem", "."],
  "enablecollapsing": true,
  "description": "File operations inside the workspace",
  "functionResult": "Working directory: /workspace",
  "systemPrompt": "Never write outside /workspace."
}
```

`functionResult` is appended to the one-time expansion result. `systemPrompt` is injected persistently while the container is expanded. Both require collapsing.

## Harness-Owned MCP Servers

A generated harness can contribute MCP server config. The attributed method provides server configuration; it does not become a model-facing function.

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

public sealed class ExternalTools
{
    [MCPServer(Description = "Local MCP server", CollapseWithinToolHarness = true)]
    public MCPServerConfig LocalMcp() => new()
    {
        Name = "local-stdio",
        Transport = "stdio",
        Command = "dotnet",
        Arguments = ["--info"],
        Enabled = false,
        RequiresPermission = false
    };
}
```

For harness-owned servers without a standalone manifest, configure package-wide MCP options separately:

```csharp
var agent = await new AgentBuilder()
    .WithMCPOptions(options =>
    {
        options.FailOnServerError = true;
    })
    .WithToolHarness<ExternalTools>()
    .BuildAsync();
```

## Background Invocation

MCP server processes may stay alive independently of a tool call, but that is separate from model-facing background invocation. To let the parent model launch an MCP tool and continue immediately, configure the server's invocation mode policy:

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem", "."],
  "invocationModePolicy": "modelChoice"
}
```

Policies:

| Policy | Tool Shape | Tool Result |
| --- | --- | --- |
| `synchronousOnly` | Original MCP schema | Original MCP result |
| `backgroundOnly` | Original MCP schema | Background launch receipt |
| `modelChoice` | Original MCP schema plus `invocationMode` | Original MCP result or background launch receipt |

For `modelChoice`, HPD augments each imported MCP tool schema with:

```json
{
  "invocationMode": "background"
}
```

The adapter removes `invocationMode` before calling the MCP server, so the server receives only its original arguments. Background calls return a structured receipt containing the runtime `taskId`. Completion or failure is delivered later through [Background Tasks And Notifications](../../concepts/background-tasks-and-notifications.md).

Use `toolInvocationModePolicies` when only specific MCP tools should differ from the server default. Keys are exact MCP tool names from the server's tool list:

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem", "."],
  "invocationModePolicy": "synchronousOnly",
  "toolInvocationModePolicies": {
    "search": "modelChoice",
    "long_running_index": "backgroundOnly"
  }
}
```

Resolution is exact tool override first, then the server default. If neither is configured, the server default is `synchronousOnly`.

Use `backgroundNotification` to change when background MCP tool calls wake the model:

```json
{
  "backgroundNotification": {
    "kind": "on_final_state",
    "completed": true,
    "faulted": true,
    "cancelled": false
  }
}
```

## Production Notes

Pin local server package versions in production so startup does not pull unexpected changes:

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem@1.2.3", "."]
}
```

Use `processIsolation` for stdio servers that should run inside HPD's process boundary. If isolated mode is requested without a configured process provider, HPD fails closed.

The C# MCP SDK handles MCP task results in the default synchronous tool-call path by waiting and polling for completion. HPD background invocation is an adapter-level launch mode for the parent model; it does not change the MCP transport protocol.
