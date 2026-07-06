# Hyperbolic

The Hyperbolic provider uses provider key `hyperbolic` and the `HPD-Agent.Providers.Hyperbolic` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is a Hyperbolic chat model id.

Set an API key:

```bash
export HYPERBOLIC_API_KEY="..."
```

The default endpoint is `https://api.hyperbolic.xyz/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithHyperbolic(...)`, set `Endpoint` in `ClientProviderConfig`, or use `HYPERBOLIC_ENDPOINT / HYPERBOLIC_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Hyperbolic;

var agent = await new AgentBuilder()
    .WithHyperbolic(model: "Qwen/Qwen2.5-72B-Instruct")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Hyperbolic setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "hyperbolic",
      "ModelName": "Qwen/Qwen2.5-72B-Instruct"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

## Caveats

This provider currently registers the chat family only. Other endpoint families, if the upstream service exposes them, are deferred until HPD has dedicated provider-family support for that surface.

Chat uses HPD's shared OpenAI-compatible chat-completions client. It supports token streaming, function tools, response format and tool mode, seed, max output tokens, top-p, temperature, and stop sequences where the selected model supports them.

Validation checks API key, model, and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
