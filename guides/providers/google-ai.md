# Google AI

The Google AI provider uses provider key `google-ai` and the `HPD-Agent.Providers.GoogleAI` package. `ModelName` is a Gemini model id.

Set either supported API key alias:

```bash
export GOOGLE_API_KEY="..."
# or
export GEMINI_API_KEY="..."
```

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.GoogleAI;

var agent = await new AgentBuilder()
    .WithGoogleAI(model: "gemini-2.0-flash")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Google AI setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "google-ai",
      "ModelName": "gemini-2.0-flash"
    }
  }
}
```

Prefer `GOOGLE_API_KEY` or `GEMINI_API_KEY` in public docs. `GOOGLE_AI_API_KEY` follows the provider-key naming convention, but `GOOGLE_API_KEY` and `GEMINI_API_KEY` are the provider's explicit aliases.

## Caveats

The provider validates API key and Gemini option ranges. Missing credentials fail at `BuildAsync()` before a live model call. Source comments note that not every configuration option maps cleanly through the underlying SDK, so keep provider-specific options narrow until the exact behavior is verified for your model.
