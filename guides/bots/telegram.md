# Telegram Bots

Telegram can run through a public webhook or through long polling. Use webhook mode for hosted production apps. Use polling for local development, private hosts, or environments that cannot receive public Telegram webhooks.

## Webhook Quick Start

```csharp
using HPD.Agent.Bots.Telegram;

builder.Services.AddTelegramBot(options =>
{
    options.BotToken = builder.Configuration["Telegram:BotToken"]!;
    options.SecretToken = builder.Configuration["Telegram:SecretToken"];
    options.UserName = builder.Configuration["Telegram:UserName"];
    options.AgentId = builder.Configuration["Agent:Id"] ?? "support-agent";
    options.Mode = TelegramBotMode.Webhook;
}, registerInfrastructure: true);

var app = builder.Build();

app.MapTelegramWebhook("/telegram/webhook");
```

Then set Telegram's webhook URL:

```bash
curl "https://api.telegram.org/bot$TELEGRAM_BOT_TOKEN/setWebhook" \
  -d "url=https://your-domain/telegram/webhook" \
  -d "secret_token=$TELEGRAM_WEBHOOK_SECRET_TOKEN"
```

When `SecretToken` is configured, HPD verifies Telegram's `x-telegram-bot-api-secret-token` header.

## Polling Quick Start

```csharp
builder.Services.AddTelegramBotWithPolling(options =>
{
    options.BotToken = builder.Configuration["Telegram:BotToken"]!;
    options.UserName = builder.Configuration["Telegram:UserName"];
    options.AgentId = builder.Configuration["Agent:Id"] ?? "support-agent";
    options.Mode = TelegramBotMode.Polling;
});
```

Do not map `MapTelegramWebhook(...)` in polling mode. Telegram webhooks and polling are mutually exclusive.

## Configuration

| Option | Required | Notes |
| --- | --- | --- |
| `BotToken` | Yes | Telegram bot token. Can also come from `TELEGRAM_BOT_TOKEN`. |
| `SecretToken` | Webhook optional | Verifies webhook requests. Can also come from `TELEGRAM_WEBHOOK_SECRET_TOKEN`. |
| `UserName` | Recommended | Bot username for reliable group mention detection. Can also come from `TELEGRAM_BOT_USERNAME`. |
| `ApiBaseUrl` | Optional | Defaults to Telegram API. Can also come from `TELEGRAM_API_BASE_URL`. |
| `AgentId` | Yes | HPD agent definition to run. |
| `Mode` | Yes | `Webhook`, `Polling`, or `Auto`. |
| `LongPolling` | Polling only | Timeout, limit, allowed updates, delete-webhook, and drop-pending-updates options. |
| `StreamingDebounceMs` | Optional | Controls edit cadence for streamed output. |

`Auto` only produces polling behavior when `AddTelegramBotWithPolling(...)` registered the polling service. The polling service polls only when Telegram has no webhook URL and the runtime does not look serverless.

## Conversation Behavior

Thread keys are `telegram:{ChatId}` or `telegram:{ChatId}:{MessageThreadId}`. Direct messages always process. Groups and channels should mention the bot or use a bot command mention; configure `UserName` for reliable detection.

## Capabilities And Limits

| Area | HPD Telegram behavior |
| --- | --- |
| Streaming | Edits messages as output arrives. |
| Text size | Telegram messages split around the 4096 character limit. |
| Files | Inbound files can become HPD input files; outbound post supports one document per message. |
| Cards | Render as inline keyboards where possible. |
| Callback data | Telegram callback data is limited to 64 bytes. |
| History | Telegram does not expose full historical thread listing; HPD can only use cached/runtime context. |
| Permissions | HPD permission requests are currently denied automatically in the Telegram streaming path. |

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Webhook receives 401/403 | `SecretToken` does not match Telegram's `secret_token`. |
| Polling receives nothing | A webhook may still be set; delete the webhook or enable polling cleanup options. |
| Group messages are ignored | Bot username is missing, the bot was not mentioned, or privacy settings block messages. |
