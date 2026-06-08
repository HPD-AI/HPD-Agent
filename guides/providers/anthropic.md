# Anthropic

The Anthropic provider uses provider key `anthropic` and the `HPD-Agent.Providers.Anthropic` package. `ModelName` is a Claude model id.

Set an API key:

```bash
export ANTHROPIC_API_KEY="..."
```

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Anthropic;

var agent = await new AgentBuilder()
    .WithAnthropic(model: "claude-3-5-sonnet-latest")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Anthropic setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "anthropic",
      "ModelName": "claude-3-5-sonnet-latest"
    }
  }
}
```

The default endpoint is Anthropic's API. Set `Endpoint` explicitly in `ClientProviderConfig` when you need a custom Anthropic base URL; `ANTHROPIC_ENDPOINT` is not a source-registered alias.

## Caveats

Validation checks API key, model, thinking, max-token, and prompt-cache options. Missing credentials fail at `BuildAsync()` before a live model call. Provider creation wraps the SDK chat client with a schema-fixing layer for Anthropic tool schema compatibility; treat that as a troubleshooting detail rather than something normal setup code needs to configure.
