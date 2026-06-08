# Hugging Face

The Hugging Face provider uses provider key `huggingface` and the `HPD-Agent.Providers.HuggingFace` package. `ModelName` is a Hugging Face repository id, such as `meta-llama/Meta-Llama-3-8B-Instruct`.

Set either supported token alias:

```bash
export HUGGINGFACE_API_KEY="..."
# or
export HF_TOKEN="..."
```

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.HuggingFace;

var agent = await new AgentBuilder()
    .WithHuggingFace(model: "meta-llama/Meta-Llama-3-8B-Instruct")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Hugging Face setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "huggingface",
      "ModelName": "meta-llama/Meta-Llama-3-8B-Instruct"
    }
  }
}
```

## Caveats

Validation checks API key, repository id, and generation option ranges. Missing credentials fail at `BuildAsync()` before a live model call. Provider metadata reports no function calling or vision support, so design tool-heavy agents around a provider that supports those capabilities.
