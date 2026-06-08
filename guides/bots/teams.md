# Teams Bots

Teams is different from the generated webhook adapters. The Microsoft 365 Agents SDK owns endpoint routing, token validation, activity deserialization, attachments, and proactive conversation endpoints. HPD takes over after the SDK has produced a turn.

## Quick Start

```csharp
using HPD.Agent.Bots.Teams;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHPDAgent(options =>
{
    options.ConfigureAgent = agent => agent.WithOpenAI(
        model: builder.Configuration["Agent:Model"]!,
        apiKey: builder.Configuration["OpenAI:ApiKey"]);
});

builder.AddTeamsBot(options =>
{
    options.AppId = builder.Configuration["Teams:AppId"]!;
    options.AppPassword = builder.Configuration["Teams:AppPassword"];
    options.AppTenantId = builder.Configuration["Teams:TenantId"];
    options.AppType = "SingleTenant";
    options.AgentId = builder.Configuration["Agent:Id"] ?? "support-agent";
});

var app = builder.Build();

app.MapTeamsBot();
```

`MapTeamsBot()` maps the standard Agents SDK endpoints and the proactive endpoints discovered from Teams continuation handlers.

## Configuration

| Option | Required | Notes |
| --- | --- | --- |
| `AppId` | Yes | Microsoft Entra app/client id. |
| `AgentId` | Yes | HPD agent definition to run. |
| `AppPassword` | One auth method | Client secret authentication. |
| `Certificate.CertificatePrivateKey` | One auth method | Certificate private key. |
| `Certificate.CertificateThumbprint` or `Certificate.X5c` | Certificate auth | Certificate identity material. |
| `Federated.ClientId` | One auth method | Workload identity authentication. |
| `Federated.ClientAudience` | Optional | Defaults to `api://AzureADTokenExchange`. |
| `AppType` | Optional | Defaults to `MultiTenant`; use `SingleTenant` when appropriate. |
| `AppTenantId` | Single tenant | Required when `AppType` is `SingleTenant`. |
| `UserName` | Optional | Bot display name used for proactive DM creation. |

Exactly one auth method should be configured: client secret, certificate, or federated workload identity.

## Microsoft 365 Agents SDK Settings

The host app must also provide the Agents SDK auth settings expected by `Microsoft.Agents.Hosting.AspNetCore`. The exact values depend on your tenant and app registration, but the shape mirrors Microsoft 365 Agents samples:

```json
{
  "Teams": {
    "AppId": "<app-client-id>",
    "AppPassword": "<client-secret>",
    "TenantId": "<tenant-id>"
  },
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<app-client-id>"],
    "TenantId": "<tenant-id>"
  },
  "AgentApplication": {
    "StartTypingTimer": false,
    "RemoveRecipientMention": false,
    "NormalizeMentions": false
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenant-id>",
        "ClientId": "<app-client-id>",
        "ClientSecret": "<client-secret>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    {
      "ServiceUrl": "*",
      "Connection": "ServiceConnection"
    }
  ]
}
```

Use the Teams Toolkit or Teams CLI workflow to create/sideload the app and verify endpoint reachability. HPD does not currently document arbitrary custom Teams endpoint aliases; use `app.MapTeamsBot()`.

## Capabilities

| Area | HPD Teams behavior |
| --- | --- |
| Inbound messages | Run the configured HPD agent. |
| Streaming | Native Teams output path. |
| Files | File uploads can be downloaded through M365 attachment downloaders and surfaced as HPD input files. |
| Cards | Adaptive Cards. |
| Card actions | Supported through Teams action handling. |
| Permissions | Approve/Deny card actions use `hpd.permission.approve` and `hpd.permission.deny`. |
| Reactions | Receiving reactions is supported; adding/removing reactions is not. |
| History | Graph-backed history is opt-in. |

## Graph History

Register Graph history only when the host already has a configured `GraphServiceClient`:

```csharp
builder.Services.AddTeamsGraphHistory(graphClient);
```

Graph-backed history uses Teams-specific fetch/list options and requires the tenant/app permissions your Graph client needs. Validate Graph permissions in the target tenant before promising history behavior to users.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Unauthorized | Client secret/certificate/federated identity is wrong or expired. |
| Single-tenant auth fails | `AppTenantId` is missing or does not match the app registration. |
| Messages do not reach HPD | Agents SDK `TokenValidation`, `Connections`, or `ConnectionsMap` is missing; app is not installed; endpoint is not public. |
| File uploads missing | M365 attachment downloaders or service permissions are not configured. |
| Graph history unavailable | `AddTeamsGraphHistory(...)` was not registered or Graph permissions are missing. |
