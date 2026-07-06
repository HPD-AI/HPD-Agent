# DeepInfra

The DeepInfra provider uses provider key `deepinfra` and the `HPD-Agent.Providers.DeepInfra` package. `ModelName` is a DeepInfra OpenAI-compatible chat model id.

Set an API key:

```bash
export DEEPINFRA_API_KEY="..."
```

The default endpoint is `https://api.deepinfra.com/v1/openai/`, which sends chat requests to `chat/completions`. Pass `endpoint` to `WithDeepInfra(...)`, set `Endpoint` in `ClientProviderConfig`, or use `DEEPINFRA_ENDPOINT` / `DEEPINFRA_BASE_URL` when you need a compatible proxy or custom base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.DeepInfra;

var agent = await new AgentBuilder()
    .WithDeepInfra(model: "meta-llama/Meta-Llama-3-8B-Instruct")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about DeepInfra setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "deepinfra",
      "ModelName": "meta-llama/Meta-Llama-3-8B-Instruct"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

## Caveats

This provider currently registers the chat family only. DeepInfra also exposes embeddings through its OpenAI-compatible API, but the HPD provider defers embeddings until the shared provider abstractions have a clean embedding base.

DeepInfra chat uses HPD's shared OpenAI-compatible chat-completions client. It supports token streaming, function tools, response format and tool mode, seed, max output tokens, top-p, temperature, and stop sequences where the selected DeepInfra model supports them.

Validation checks API key, model, and endpoint configuration. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
