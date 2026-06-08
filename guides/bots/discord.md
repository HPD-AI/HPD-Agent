# Discord Bots

Discord has two useful entry points. Interaction webhooks handle pings, slash commands, buttons, modals, and autocomplete. Gateway mode is required for regular message and reaction events.

## Quick Start

```csharp
using HPD.Agent.Bots.Discord;

builder.Services.AddDiscordBot(options =>
{
    options.PublicKey = builder.Configuration["Discord:PublicKey"]!;
    options.BotToken = builder.Configuration["Discord:BotToken"]!;
    options.ApplicationId = builder.Configuration["Discord:ApplicationId"]!;
    options.AgentId = builder.Configuration["Agent:Id"] ?? "support-agent";
    options.GatewayToken = builder.Configuration["Discord:GatewayToken"];
    options.GatewayForwardUrl = builder.Configuration["Discord:GatewayForwardUrl"];
}, registerInfrastructure: true);

var app = builder.Build();

app.MapDiscordWebhook("/discord/interactions");
```

`AgentId` is the HPD agent definition Discord should invoke.

## Discord App Setup

In the Discord Developer Portal:

1. Create an application.
2. Copy the Application ID into `Discord:ApplicationId`.
3. Copy the Public Key into `Discord:PublicKey`.
4. Reset and copy the bot token into `Discord:BotToken`.
5. Set the Interactions Endpoint URL to `https://your-domain/discord/interactions`.
6. Invite the bot with `bot` and `applications.commands` scopes.

For Gateway message handling, enable the intents your app needs. Message-based HPD runs usually require server or DM message events plus Message Content Intent. Grant channel permissions such as Send Messages, Send Messages in Threads, Create Public Threads, Read Message History, Add Reactions, and Attach Files as needed.

## Configuration

| Option | Required | Notes |
| --- | --- | --- |
| `PublicKey` | Yes | Verifies Discord interaction signatures. |
| `BotToken` | Yes | Used for follow-up messages and Discord API calls. |
| `ApplicationId` | Yes | Discord application id. |
| `AgentId` | Yes | HPD agent definition to run. |
| `GatewayToken` | Gateway only | Enables the hosted Gateway service. Often the same value as `BotToken`. |
| `GatewayForwardUrl` | Gateway only | Absolute public URL for the mapped interaction endpoint. |
| `GatewaySessionDuration` | Optional | Gateway session lifetime. |
| `MentionRoleIds` | Optional | Role ids that should trigger the bot when mentioned. |
| `StreamingDebounceMs` | Optional | Controls output edit cadence. |

## Gateway Mode

Gateway mode forwards selected Gateway events into the same generated dispatch path as the webhook endpoint. Configure:

```csharp
options.GatewayToken = builder.Configuration["Discord:GatewayToken"];
options.GatewayForwardUrl = "https://your-domain/discord/interactions";
```

Use Gateway when you want normal messages, role mentions, or reaction-style events. Slash commands and interaction callbacks do not require Gateway.

## Runtime Behavior

Slash commands are deferred and streamed by editing the interaction response. Gateway mentions can create or reuse a Discord thread and bind it to the HPD session. Discord cards render as embeds with buttons or link buttons.

Discord permission requests are currently not an approval-button flow. Do not promise interactive permission approval for Discord until the adapter implements it.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Endpoint verification fails | `Discord:PublicKey` is wrong or the request body/signature is not preserved. |
| Slash commands work but messages do not | Gateway is not configured, intents are missing, or Message Content Intent is disabled. |
| Gateway starts but no events reach HPD | `GatewayForwardUrl` is missing, wrong, private, or not mapped to `MapDiscordWebhook(...)`. |
