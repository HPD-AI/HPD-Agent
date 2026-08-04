# Groq

The Groq provider uses provider key `groq` and the `HPD-Agent.Providers.Groq` package. `ModelName` is a Groq chat model id.

The provider package is currently `net10.0` only and uses Groq's OpenAI-compatible chat completions API through HPD-Agent's shared OpenAI-compatible client.

Set an API key:

```bash
export GROQ_API_KEY="..."
```

The default endpoint is `https://api.groq.com/openai/v1/`. Pass `endpoint` to `WithGroq(...)`, set `Endpoint` in `ChatClientConfig`, or set `GROQ_ENDPOINT` when you need a compatible proxy or custom Groq base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Groq;

var agent = await new AgentBuilder()
    .WithGroq(model: "llama-3.3-70b-versatile")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Groq setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "groq",
      "ModelName": "llama-3.3-70b-versatile"
    }
  }
}
```

## Runtime Chat Options

Use `ChatClientConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `AgentConfig.Clients.Chat`; use `AgentRunConfig.Clients.Chat` for complete per-run replacements.

## Caveats

Groq chat is available only when the `HPD-Agent.Providers.Groq` package is referenced by a `net10.0` application.

Groq chat supports token streaming, function tools, reasoning-capable models, and JSON object responses through the shared OpenAI-compatible chat completions client. Embeddings, rerank, batch, files, and audio families are not registered by this provider.

Runtime validation checks model and endpoint configuration. Runtime chat options are applied through `Clients.Chat` and `AgentRunConfig.Clients.Chat`. Credentials are resolved lazily when a run first acquires this provider client; a missing credential fails that run, not `BuildAsync()`.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
