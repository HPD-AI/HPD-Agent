# Anthropic

The Anthropic provider uses provider key `anthropic` and the `HPD-Agent.Providers.Anthropic` package. `ModelName` is a Claude model id.

Set an API key:

```bash
export ANTHROPIC_API_KEY="..."
```

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Anthropic;

var agent = await new AgentBuilder()
    .WithAnthropic(model: "claude-3-5-sonnet-latest")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Anthropic setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "anthropic",
      "ModelName": "claude-3-5-sonnet-latest"
    }
  }
}
```

The default endpoint is Anthropic's API. Set `Endpoint` explicitly in `ClientProviderConfig` when you need a custom Anthropic base URL; `ANTHROPIC_ENDPOINT` is not a source-registered alias.

## Runtime Chat Options

Use HPD `ChatRunConfig` for normal model-call behavior. Agent-level defaults belong in `ClientProviderConfig.ChatDefaults`; per-run or per-session overrides belong in `AgentRunConfig.Chat`.

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Anthropic;
var agent = await new AgentBuilder()
    .WithAnthropic(model: "claude-3-5-sonnet-latest")
    .WithChatDefaults(new ChatRunConfig
    {
        Temperature = 0.2,
        MaxOutputTokens = 4096,
        Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        }
    })
    .BuildAsync();
```

Use runtime chat options for model selection, temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options.

## Anthropic Chat Request Options

Use `AnthropicChatRequestOptions` for Anthropic-only request properties:

| Option | Purpose |
| --- | --- |
| `ServiceTier` | Sets Anthropic request service tier, such as `auto` or `standard_only`. |
| `ThinkingBudgetTokens` | Sets an exact Anthropic extended-thinking token budget. Use generic `ChatRunConfig.Reasoning` for normal reasoning control. |
| `ThinkingDisplay` | Controls Anthropic thinking display, such as `summarized` or `omitted`, when using `ThinkingBudgetTokens`. |
| `CacheControl` | Adds Anthropic prompt-cache markers to supported text content blocks. |

`CacheControl` has its own options:

| Option | Purpose |
| --- | --- |
| `SystemMessages` | Applies Anthropic prompt-cache control to system-message text blocks. |
| `LastUserMessage` | Applies Anthropic prompt-cache control to text blocks on the final user message in the request. |

Cache TTL values are `FiveMinutes` (`5m` in JSON) and `OneHour` (`1h` in JSON).

In C#:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Anthropic;

var agent = await new AgentBuilder()
    .WithAnthropic(model: "claude-3-5-sonnet-latest")
    .WithAnthropicChatRequestOptions(anthropic =>
    {
        anthropic.ServiceTier = AnthropicServiceTier.Auto;
        anthropic.CacheControl = new AnthropicCacheControlConfig
        {
            SystemMessages = AnthropicCacheTtl.OneHour,
            LastUserMessage = AnthropicCacheTtl.FiveMinutes
        };
    })
    .BuildAsync();
```

Per run:

```csharp
var runConfig = new AgentRunConfig
{
    Chat = new ChatRunConfig
    {
        Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        }
    }
};

runConfig.Chat.UseAnthropicChatRequestOptions(new AnthropicChatRequestOptions
{
    ServiceTier = AnthropicServiceTier.Auto,
    ThinkingBudgetTokens = 4096,
    ThinkingDisplay = AnthropicThinkingDisplay.Summarized
});
```

In stored config, put Anthropic request options in chat defaults or per-run chat additional properties, not in `ProviderOptions`:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "anthropic",
      "ModelName": "claude-3-5-sonnet-latest",
      "ChatDefaults": {
        "AdditionalProperties": {
          "serviceTier": "auto",
          "thinkingBudgetTokens": 4096,
          "thinkingDisplay": "summarized",
          "cacheControl": {
            "systemMessages": "1h",
            "lastUserMessage": "5m"
          }
        }
      }
    }
  }
}
```

## Caveats

Validation checks API key, model, and endpoint. Missing credentials fail at `BuildAsync()` before a live model call. Anthropic chat uses the Anthropic SDK's MEAI chat client.
