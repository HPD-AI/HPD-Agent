# Perplexity

The Perplexity provider uses provider key `perplexity` and the `HPD-Agent.Providers.Perplexity` package. It is backed by HPD's shared OpenAI-compatible chat-completions client, so `ModelName` is a Perplexity chat model id.

Set an API key:

```bash
export PERPLEXITY_API_KEY="..."
```

The default endpoint is `https://api.perplexity.ai/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithPerplexity(...)`, set `Endpoint` in `ClientProviderConfig`, or use `PERPLEXITY_ENDPOINT / PERPLEXITY_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Perplexity;

var agent = await new AgentBuilder()
    .WithPerplexity(model: "sonar-pro")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Perplexity setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "perplexity",
      "ModelName": "sonar-pro"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

## Caveats

Perplexity responses may include citations and search-grounding metadata. The shared OpenAI-compatible client maps OpenAI-style `annotations`, top-level `citations`, and `search_results` into MEAI `CitationAnnotation` entries when those fields are present.

This provider currently registers the chat family only. Other endpoint families, if the upstream service exposes them, are deferred until HPD has dedicated provider-family support for that surface.

Chat uses HPD's shared OpenAI-compatible chat-completions client. It supports token streaming, function tools, response format and tool mode, multiple tool calls, MEAI image content parts, reasoning effort/content, seed, max output tokens, top-p, temperature, and stop sequences where the selected model supports them.

Validation checks model and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
