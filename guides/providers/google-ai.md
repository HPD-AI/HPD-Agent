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

Runtime chat behavior such as temperature, top-p, top-k, max output tokens, stop sequences, JSON output, tools, and reasoning belongs in `ChatDefaults` or per-run `AgentRunConfig.Chat`:

```csharp
var agent = await new AgentBuilder()
    .WithGoogleAI(model: "gemini-2.0-flash")
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.TopP = 0.95;
        chat.MaxOutputTokens = 4096;
    })
    .BuildAsync();
```

`GoogleAIProviderConfig` is for Google-native provider construction:

```csharp
var agent = await new AgentBuilder()
    .WithGoogleAI(
        model: "gemini-2.0-flash",
        configure: google =>
        {
            google.ApiVersion = "v1beta";
        })
    .BuildAsync();
```

For Vertex AI:

```csharp
var agent = await new AgentBuilder()
    .WithGoogleAI(
        model: "gemini-2.0-flash",
        configure: google =>
        {
            google.Platform = GoogleAIPlatform.VertexAI;
            google.ProjectId = "my-project";
            google.Region = "us-central1";
            google.ApiVersion = "v1beta1";
            google.CredentialsFile = "/path/to/application_default_credentials.json";
        })
    .BuildAsync();
```

Use `ExpressMode = true` for Vertex AI Express mode. Express mode uses API-key authentication, so configure an API key the same way as the Gemini Developer API path.

## Provider Options

`GoogleAIProviderConfig` is for Google-native client construction and authentication setup:

| Option | Purpose |
| --- | --- |
| `Platform` | Selects the Google adapter. `GeminiDeveloperApi` uses the Gemini Developer API with API-key auth; `VertexAI` uses Vertex AI. |
| `ApiVersion` | Selects the Google API version, such as `v1`, `v1beta`, or `v1beta1`. |
| `ProjectId` | Google Cloud project id for Vertex AI. |
| `Region` | Google Cloud region for Vertex AI, such as `us-central1` or `global`. |
| `ExpressMode` | Enables Vertex AI Express mode. Express mode uses API-key authentication. |
| `CredentialsFile` | Path to an Application Default Credentials JSON file for Vertex AI. |
| `ValidateAccessToken` | Controls whether the underlying adapter validates supplied access tokens. Defaults to `true`. |

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

With provider options:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "google-ai",
      "ModelName": "gemini-2.0-flash",
      "ProviderOptions": {
        "platform": "VertexAI",
        "projectId": "my-project",
        "region": "us-central1",
        "apiVersion": "v1beta1",
        "credentialsFile": "/path/to/application_default_credentials.json"
      }
    }
  }
}
```

Prefer `GOOGLE_API_KEY` or `GEMINI_API_KEY` for Gemini Developer API credentials. `GOOGLE_AI_API_KEY` is also registered as the HPD provider-key convention alias.
