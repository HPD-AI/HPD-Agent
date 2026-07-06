# Nscale

The Nscale provider uses provider key `nscale` and the `HPD-Agent.Providers.Nscale` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is a Nscale chat model id.

Set an API key:

```bash
export NSCALE_API_KEY="..."
```

The default endpoint is `https://inference.api.nscale.com/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithNscale(...)`, set `Endpoint` in `ClientProviderConfig`, or use `NSCALE_ENDPOINT / NSCALE_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Nscale;

var agent = await new AgentBuilder()
    .WithNscale(model: "Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Nscale setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "nscale",
      "ModelName": "Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8"
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
