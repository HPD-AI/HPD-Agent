# xAI

The xAI provider uses provider key `xai` and the `HPD-Agent.Providers.Xai` package. It is backed by the shared OpenAI-compatible chat-completions client, so `ModelName` is an xAI chat model id such as `grok-4.3`.

Set an API key:

```bash
export XAI_API_KEY="..."
```

The default endpoint is `https://api.x.ai/v1/`. Pass `endpoint` to `WithXai(...)`, set `Endpoint` in `ClientProviderConfig`, or use `XAI_ENDPOINT`/`XAI_BASE_URL` when you need a compatible proxy or custom xAI base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Xai;

var agent = await new AgentBuilder()
    .WithXai(model: "grok-4.3")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about xAI setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "xai",
      "ModelName": "grok-4.3"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

`ResponseFormat` accepts `text` or `json_object`. Per-run HPD chat options can supply JSON schema response format and tool declarations where the model supports them.

## Caveats

This package currently registers only the chat family. xAI image generation, image editing, embeddings, audio, realtime voice, files, batches, and deferred completions are deferred until those families have dedicated HPD provider support.

Validation checks API key, model, and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
