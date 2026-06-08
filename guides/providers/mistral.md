# Mistral

The Mistral provider uses provider key `mistral` and the `HPD-Agent.Providers.Mistral` package. `ModelName` is a Mistral model id.

Set an API key:

```bash
export MISTRAL_API_KEY="..."
```

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Mistral;

var agent = await new AgentBuilder()
    .WithMistral(model: "mistral-large-latest")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Mistral setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "mistral",
      "ModelName": "mistral-large-latest"
    }
  }
}
```

## Caveats

Validation checks API key, model, temperature, top-p, response format, and tool choice options. Missing credentials fail at `BuildAsync()` before a live model call. Provider metadata reports function calling support and no vision support.
