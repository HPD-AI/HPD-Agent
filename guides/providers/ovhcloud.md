# OVHcloud AI Endpoints

The OVHcloud provider uses provider key `ovhcloud` and the `HPD-Agent.Providers.OVHcloud` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is an OVHcloud chat model id.

Set an API key:

```bash
export OVHCLOUD_API_KEY="..."
```

The default endpoint is `https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithOVHcloud(...)`, set `Endpoint` in `ClientProviderConfig`, or use `OVHCLOUD_ENDPOINT / OVHCLOUD_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OVHcloud;

var agent = await new AgentBuilder()
    .WithOVHcloud(model: "gpt-oss-120b")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about OVHcloud setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "ovhcloud",
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
