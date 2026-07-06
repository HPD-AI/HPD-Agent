# Moonshot

The Moonshot provider uses provider key `moonshot` and the `HPD-Agent.Providers.Moonshot` package. It is backed by the shared OpenAI-compatible chat-completions client, so `ModelName` is a Moonshot or Kimi chat model id such as `kimi-k2.5`, `kimi-k2.6`, or `moonshot-v1-128k`.

Set an API key:

```bash
export MOONSHOT_API_KEY="..."
```

`KIMI_API_KEY` is also accepted. The default endpoint is `https://api.moonshot.ai/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithMoonshot(...)`, set `Endpoint` in `ClientProviderConfig`, or use `MOONSHOT_ENDPOINT`, `MOONSHOT_BASE_URL`, `KIMI_ENDPOINT`, or `KIMI_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Moonshot;

var agent = await new AgentBuilder()
    .WithMoonshot(model: "kimi-k2.5")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Moonshot setup.");
Console.WriteLine(result.Text);
```

Provider construction stays small. Put model behavior on chat defaults or per-run chat config, and use Moonshot request options only for Kimi-specific request fields:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Moonshot;

var agent = await new AgentBuilder()
    .WithMoonshot(model: "kimi-k2.6")
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.MaxOutputTokens = 4096;
        chat.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        };
    })
    .WithMoonshotChatRequestOptions(moonshot =>
    {
        moonshot.ThinkingKeep = MoonshotThinkingKeep.All;
    })
    .BuildAsync();
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "moonshot",
      "ModelName": "kimi-k2.6",
      "ChatDefaults": {
        "Temperature": 0.2,
        "MaxOutputTokens": 4096,
        "Reasoning": {
          "Effort": "High"
        },
        "AdditionalProperties": {
          "thinking_keep": "all"
        }
      }
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

Moonshot/Kimi thinking type is derived from `ChatRunConfig.Reasoning` when the selected model supports Kimi thinking.

Use `MoonshotChatRequestOptions` for Moonshot/Kimi-only request properties:

| Option | Moonshot key | Meaning |
| --- | --- | --- |
| `ThinkingKeep` | `thinking_keep` | Preserves reasoning content from historical assistant messages. Supported value: `all`. |

## Moonshot Provider Options

Moonshot currently has no provider-specific construction options. Use `ClientProviderConfig.Endpoint`, the fluent `endpoint` argument, or endpoint environment variables for custom base URLs.

Use `ChatRunConfig.ResponseFormat` for text, JSON object, or JSON schema response formatting where the selected Moonshot/Kimi model supports it.

## Caveats

This package currently registers only the chat family. Moonshot files, embeddings, media upload, token counting, billing, batches, and model listing are deferred until those families have dedicated HPD provider support.

Moonshot chat uses HPD's shared OpenAI-compatible chat-completions client, not the generated Moonshot SDK. The generated SDK is useful as a reference for Kimi request fields such as `thinking`, but HPD sends chat through the OpenAI-compatible endpoint. It supports token streaming, function tools, response format and tool mode, seed, max output tokens, top-p, temperature, and stop sequences where the selected Moonshot/Kimi model supports them. `ChatRunConfig.Reasoning` and `MoonshotChatRequestOptions.ThinkingKeep` add a Kimi `thinking` object to the request for models that support those fields.

Validation checks API key and endpoint shape. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
