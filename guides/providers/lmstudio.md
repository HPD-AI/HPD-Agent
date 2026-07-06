# LM Studio

The LM Studio provider uses provider key `lmstudio` and the `HPD-Agent.Providers.LMStudio` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is a LM Studio chat model id.

Start the LM Studio local server. An API key is not required for the default local endpoint.

The default endpoint is `http://localhost:1234/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithLMStudio(...)`, set `Endpoint` in `ClientProviderConfig`, or use `LMSTUDIO_ENDPOINT / LMSTUDIO_BASE_URL / LMSTUDIO_API_BASE / LM_STUDIO_ENDPOINT / LM_STUDIO_BASE_URL / LM_STUDIO_API_BASE` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.LMStudio;

var agent = await new AgentBuilder()
    .WithLMStudio(model: "local-model")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about LM Studio setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "lmstudio",
      "ModelName": "local-model"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

## Caveats

LM Studio is local by default and does not require an API key. If your local server or proxy requires one, pass `apiKey` explicitly or set one of the optional `LMSTUDIO_API_KEY / LM_STUDIO_API_KEY` aliases.

This provider currently registers the chat family only. Other endpoint families, if the upstream service exposes them, are deferred until HPD has dedicated provider-family support for that surface.

Chat uses HPD's shared OpenAI-compatible chat-completions client. It supports token streaming, function tools, response format and tool mode, seed, max output tokens, top-p, temperature, and stop sequences where the selected model supports them.

Validation checks model and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials are allowed for the default local endpoint.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
