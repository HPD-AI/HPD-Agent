# Fireworks AI

The Fireworks AI provider uses provider key `fireworks` and the `HPD-Agent.Providers.Fireworks` package. `ModelName` is a Fireworks chat model id.

The provider package is currently `net10.0` only and uses HPD Agent's shared OpenAI-compatible chat-completions client.

Set an API key:

```bash
export FIREWORKS_API_KEY="..."
```

The default endpoint is `https://api.fireworks.ai/inference/v1/`. Pass `endpoint` to `WithFireworks(...)`, set `Endpoint` in `ClientProviderConfig`, or use `FIREWORKS_ENDPOINT` / `FIREWORKS_BASE_URL` when you need a compatible proxy or custom Fireworks base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Fireworks;

var agent = await new AgentBuilder()
    .WithFireworks(model: "accounts/fireworks/models/llama-v3p1-8b-instruct")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Fireworks AI setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "fireworks",
      "ModelName": "accounts/fireworks/models/llama-v3p1-8b-instruct"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

Supported `ResponseFormat` values are `text` and `json_object`. Supported `ToolChoice` values are `auto`, `none`, and `required`.

## Caveats

This package registers chat only. Fireworks audio/STT, embeddings, image generation, and hosted-file families are not registered by this provider.

Validation checks API key, model, and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
