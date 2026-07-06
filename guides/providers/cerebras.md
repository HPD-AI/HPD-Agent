# Cerebras

The Cerebras provider uses provider key `cerebras` and the `HPD-Agent.Providers.Cerebras` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is a Cerebras chat model id.

Set an API key:

```bash
export CEREBRAS_API_KEY="..."
```

The default endpoint is `https://api.cerebras.ai/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithCerebras(...)`, set `Endpoint` in `ClientProviderConfig`, or use `CEREBRAS_ENDPOINT / CEREBRAS_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Cerebras;

var agent = await new AgentBuilder()
    .WithCerebras(model: "gpt-oss-120b")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Cerebras setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "cerebras",
      "ModelName": "gpt-oss-120b"
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
