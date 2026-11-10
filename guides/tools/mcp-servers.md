# MCP Servers

Use `HPD-Agent.MCP` to consume Model Context Protocol servers as HPD Agent
capabilities. MCP tools, resources, and prompts become HPD-managed
`AIFunction`s and participate in the same visibility, permission, retry, and
operation systems as native tools.

The package targets .NET 8, .NET 9, and .NET 10.

## Install

```bash
dotnet add package HPD-Agent.MCP --version 0.9.5
dotnet add package HPD-Agent.Providers.OpenAI --version 0.9.5
```

`HPD-Agent.MCP` brings in `HPD-Agent.Framework`. Install a provider package or
configure an `IChatClient` directly.

## Quick Start

Create `mcp.json`:

```json
{
  "servers": [
    {
      "name": "filesystem",
      "transport": "stdio",
      "command": "npx",
      "arguments": [
        "-y",
        "@modelcontextprotocol/server-filesystem@1.2.3",
        "/workspace"
      ],
      "permission": {
        "requiresPermission": false
      }
    }
  ]
}
```

Register the manifest:

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

await using var agent = await new AgentBuilder(new AgentConfig
{
    Name = "assistant"
})
    .WithChatClient(chatClient)
    .WithMcp("mcp.json")
    .BuildAsync();

var sessionId = await agent.CreateSessionAsync();
var result = await agent.RunAsync(
    "List the files available through MCP.",
    sessionId);
```

The quick-start manifest disables permission so it can run without an approval
UI. Keep the default permission behavior for real use.

## Permission Defaults

MCP tools require permission by default. A protected source fails publication
when the agent does not contain permission middleware.

```csharp
await using var agent = await new AgentBuilder(config)
    .WithChatClient(chatClient)
    .WithPermissions()
    .WithMcp("mcp.json")
    .BuildAsync();
```

Handle `PermissionRequestEvent` with `agent.AnswerRequestAsync(...)`. See
[Permissions Middleware](../middleware/permissions.md) for approval and denial
handling.

Permission and allowlist configuration belongs to each server:

```json
{
  "servers": [
    {
      "name": "filesystem",
      "transport": "stdio",
      "command": "npx",
      "arguments": ["-y", "@modelcontextprotocol/server-filesystem@1.2.3", "/workspace"],
      "allowedTools": ["read_file", "list_directory"],
      "permission": {
        "requiresPermission": true,
        "policy": "hpd.mcp.filesystem.v1",
        "tools": {
          "list_directory": {
            "requiresPermission": false
          }
        }
      }
    }
  ]
}
```

`allowedTools` is an exact allowlist of original MCP tool names. Omitting it
admits every tool returned by the server, subject to the permission policy.

## Streamable HTTP

Use `transport: "http"` and an absolute HTTP(S) `endpoint`:

```json
{
  "servers": [
    {
      "name": "search",
      "transport": "http",
      "endpoint": "https://mcp.example.com/mcp",
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

Use literal `headers` for non-secret values and `headerSecretKeys` for values
resolved through `ISecretResolver`. MCP protocol headers, transport session IDs,
and bearer values cannot be supplied through the manifest.

Protocol negotiation uses SDK discovery first and may fall back to the
initialize-era protocol. Set `exactVersion` only when a server requires an exact
revision.

## Registration Policies

Configure package-wide policy with `WithMcp` or `WithMcpOptions`:

```csharp
await using var agent = await new AgentBuilder(config)
    .WithChatClient(chatClient)
    .WithMcp("mcp.json", options =>
    {
        options.FailOnServerError = true;
        options.Protocol.DiscoveryTimeout = TimeSpan.FromSeconds(5);
        options.Catalog.LoadMode = McpCatalogLoadMode.Eager;
        options.Catalog.StaleRetention = TimeSpan.FromMinutes(5);
    })
    .BuildAsync();
```

Required sources load during agent construction. Use
`McpCatalogLoadMode.Deferred` only when loading at the first capability boundary
is acceptable.

Set `FailOnServerError = false` to retain successful servers when another server
fails. The default is fail-fast.

Manifest JSON can be provided directly:

```csharp
builder.WithMcpContent(
    manifestJson,
    sourceName: "application-mcp",
    options => options.Catalog.LoadMode = McpCatalogLoadMode.Deferred);
```

The source name is a stable identity, not the server display name.

## Agent Configuration

Inline servers and external manifests can also be declared through `AgentConfig`:

```yaml
name: McpAgent
mcp:
  servers:
    - name: local
      transport: stdio
      command: mcp-server
  manifests:
    - path: ./config/mcp.json
      sourceName: application-mcp
```

Relative manifest paths resolve from the source configuration document. Duplicate
server names across inline and file sources fail configuration validation.

## OAuth

MCP OAuth configuration remains in the server manifest and never contains token
state:

```json
{
  "servers": [
    {
      "name": "enterprise",
      "transport": "http",
      "endpoint": "https://mcp.example.com/mcp",
      "oauth": {
        "registrationMode": "ClientIdMetadataDocument",
        "redirectUri": "http://127.0.0.1:43110/callback",
        "clientIdMetadataDocument": "https://client.example.com/oauth/metadata.json",
        "scopes": ["read", "write"]
      }
    }
  ]
}
```

Pre-registered clients use `registrationMode: "PreRegistered"` with `clientId`
and an optional `clientSecretKey`. Dynamic Client Registration is compatibility
only and requires both:

```json
{
  "registrationMode": "DynamicRegistration",
  "allowDynamicRegistration": true
}
```

Durable authorization state is application-owned. Implement
`IMcpAuthorizationStore` and pass it to the builder:

```csharp
.WithMcp("mcp.json", options =>
{
    options.AuthorizationStore = applicationAuthorizationStore;
})
```

Records are bound to the resource identity, authorization-server issuer, client
identity, and normalized scopes. Mismatched records fail closed.

## Resources And Prompts

Resources and prompts are projected only when enabled:

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

Resource and prompt text is bounded. Binary resource content returns metadata
instead of unbounded base64 payloads.

## MRTR Input

SDK 2.x owns `input_required` reconstruction, retries, cancellation, and the
round limit. HPD supplies the application policy:

```csharp
.WithMcp("mcp.json", options =>
{
    options.Invocation.InputResolver = inputResolver;
    options.Invocation.InputAuthorizer = inputAuthorizer;
    options.Invocation.MaxInputPayloadCharacters = 200_000;
    options.Invocation.HandlerTimeout = TimeSpan.FromMinutes(10);
})
```

An unresolved or unauthorized input request never becomes successful tool output.

## Local Background Work And Remote Tasks

HPD local background invocation and provider-owned remote MCP Tasks are distinct:

- HPD background work is controlled by the application's operation registry.
- Remote MCP Tasks are server-owned jobs observed through that registry.

Local invocation policy is configured per server or exact tool:

```json
{
  "name": "search",
  "transport": "http",
  "endpoint": "https://mcp.example.com/mcp",
  "invocationModePolicy": "modelChoice",
  "toolInvocationModePolicies": {
    "long_running_index": "backgroundOnly"
  }
}
```

Policies are `synchronousOnly`, `backgroundOnly`, and `modelChoice`. In
`modelChoice`, HPD adds an `invocationMode` field and removes it before calling
the server.

Remote MCP Tasks are inert until explicitly enabled:

```csharp
.WithMcp("mcp.json", options =>
{
    options.AddTasksExtension();
    options.Invocation.RecoveryReferenceProtector = recoveryProtector;
})
```

The resulting operation keeps HPD's `OperationId` separately from the provider
task ID. Detaching local observation does not imply remote cancellation.

## Catalog Refresh And Subscriptions

Catalog pages honor server TTL and cache-scope facts. HPD limits TTL, coalesces
refreshes, and may serve scope-compatible stale pages after a transient failure.

```csharp
.WithMcp("mcp.json", options =>
{
    options.Catalog.MaximumTtl = TimeSpan.FromHours(1);
    options.Catalog.RefreshOnInvalidation = true;
    options.Catalog.ServeStaleOnTransientFailure = true;
    options.Subscriptions.EnableCatalogInvalidation = true;
    options.Subscriptions.ResourceUris = ["file:///workspace/README.md"];
    options.Subscriptions.FailurePolicy = McpSubscriptionFailurePolicy.ReportAndContinue;
})
```

Notifications make a source eligible for transactional refresh. They never
mutate the tool list of a turn already in progress.

## Isolated Stdio

`processIsolation` changes the process launch and byte ownership path:

```json
{
  "name": "filesystem",
  "transport": "stdio",
  "command": "npx",
  "arguments": ["-y", "@modelcontextprotocol/server-filesystem@1.2.3", "/workspace"],
  "processIsolation": {
    "enabled": true,
    "allowNetwork": false,
    "allowedPaths": ["/workspace"],
    "maxMessageBytes": 4194304
  }
}
```

An application-provided `IProcessProvider` is required:

```csharp
.WithMcp("mcp.json", options =>
{
    options.ProcessProvider = applicationProcessProvider;
})
```

When isolation is enabled without a provider, agent construction fails closed.
HTTP servers cannot use process isolation.

## Harness-Owned Servers

A generated ToolHarness can contribute an MCP server. The method returns server
configuration and does not become a model-facing function:

```csharp
using HPD.Agent;
using HPD.Agent.MCP;

public sealed class ExternalTools
{
    [McpServer(Name = "local")]
    [RequiresPermission]
    public McpServerConfig Local() => new()
    {
        Name = "local",
        Transport = "stdio",
        Command = "mcp-server"
    };
}
```

Use `WithMcpOptions(...)` for package-wide policy before registering the harness.
Set `CollapseWithinToolHarness = true` when the MCP tools should sit behind their
own `MCP_{server}` container inside the harness.

## Native AOT And Public API

Declarative `AgentConfig.mcp` and source-generated `[McpServer]` are the Native
AOT paths. The package has a publish-and-run Native AOT gate covering discovery,
tool listing, invocation, permissions, serialization, and shutdown.

Application code should use builder extensions, `McpOptions`, resolver/store
interfaces, and `AddTasksExtension`. The raw runtime, serializer context,
projection models, transport framing, and catalog cache are internal.

Generated support types remain public because consumer-generated code references
them, but they are hidden from IntelliSense and are not application APIs.

## Troubleshooting

- `McpToolProtectedWithoutPermissionMiddleware`: add `.WithPermissions()` and
  handle permission requests.
- `McpManifestNotFound` or `McpManifestUnreadable`: verify the manifest path is
  relative to the owning agent configuration document.
- `McpServerNameConflict`: inline and file-backed declarations must use unique
  server names.
- Discovery failure: leave `Protocol.ExactVersion` unset so legacy fallback is
  available.
- Isolation failure: configure `ProcessProvider`; HPD never silently launches an
  isolated server without an enforcing provider.
