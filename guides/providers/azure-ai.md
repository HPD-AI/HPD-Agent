# Azure AI

The Azure AI provider uses provider key `azure-ai` and the `HPD-Agent.Providers.AzureAI` package. Use it for Azure AI Projects / Foundry endpoints and for direct Azure OpenAI-compatible endpoints when you want the Azure AI provider's authentication behavior.

For traditional Azure OpenAI resource setup through the OpenAI provider package, see [OpenAI And Azure OpenAI](openai-and-azure-openai.md).

## Setup

Set the project endpoint:

```bash
export AZURE_AI_ENDPOINT="https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT"
```

For Azure AI Projects / Foundry endpoints, use OAuth through `DefaultAzureCredential`. Configure your normal Azure identity for the host environment.

```csharp
using HPD.Agent;
using HPD.Agent.Providers.AzureAI;

var agent = await new AgentBuilder()
    .WithAzureAI(
        endpoint: "https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT",
        model: "YOUR-DEPLOYMENT-NAME",
        configure: azure =>
        {
            azure.AuthMode = AzureAIAuthMode.DefaultAzureCredential;
        })
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Azure AI setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "azure-ai",
      "Endpoint": "https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "ModelName": "YOUR-DEPLOYMENT-NAME",
      "ProviderOptions": {
        "authMode": "DefaultAzureCredential"
      }
    }
  }
}
```

## Authentication

`AzureAIProviderConfig.AuthMode` controls provider authentication:

| Value | Behavior |
| --- | --- |
| `Auto` | Uses an API key when one is configured; otherwise uses `DefaultAzureCredential`. |
| `ApiKey` | Requires `apiKey` or `AZURE_AI_API_KEY`. Only valid for direct Azure OpenAI-compatible endpoints. |
| `DefaultAzureCredential` | Always uses Azure Identity OAuth. Required for Azure AI Projects / Foundry endpoints. |

Azure AI Projects / Foundry endpoints use `services.ai.azure.com` with `/api/projects/` and require OAuth. Passing an API key for that endpoint shape is invalid.

## Runtime Chat Options

Use HPD `ChatRunConfig` for normal model-call behavior. Agent-level defaults belong in `ClientProviderConfig.ChatDefaults`; per-run or per-session overrides belong in `AgentRunConfig.Chat`.

Use runtime chat options for model selection, temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options.

Azure AI uses the Azure OpenAI chat-completions client through Microsoft.Extensions.AI. `Reasoning.Effort` is the portable reasoning option on this path. Reasoning summary output is a Responses-client feature and is not exposed by this provider path.

```csharp
var agent = await new AgentBuilder()
    .WithAzureAI(
        endpoint: "https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT",
        model: "YOUR-DEPLOYMENT-NAME",
        configure: azure =>
        {
            azure.AuthMode = AzureAIAuthMode.DefaultAzureCredential;
        })
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.MaxOutputTokens = 4096;
        chat.Reasoning = new()
        {
            Effort = ReasoningEffort.High
        };
    })
    .BuildAsync();
```

## Provider Options

`AzureAIProviderConfig` is for Azure SDK client construction:

| Option | Purpose |
| --- | --- |
| `AuthMode` | Selects API key vs `DefaultAzureCredential` behavior. |
| `ProjectServiceVersion` | Selects the Azure AI Projects SDK service version. |
| `OpenAIServiceVersion` | Selects the downstream Azure OpenAI SDK service version. |
| `OpenAIConnectionId` | Selects the project connection id used to locate the Azure OpenAI connection. |
| `OpenAIAudience` | Sets the downstream Azure OpenAI Entra audience/scope. |
| `OpenAIDefaultHeaders` | Adds default downstream Azure OpenAI request headers. |
| `OpenAIDefaultQueryParameters` | Advanced escape hatch for default downstream Azure OpenAI query parameters. Prefer `OpenAIServiceVersion` instead of setting `api-version` here. |
| `UserAgentApplicationId` | Adds an app id to Azure SDK user agents. |
| `NetworkTimeoutMs` | Sets Azure SDK pipeline network timeout. |
| `EnableDistributedTracing` | Enables or disables Azure SDK distributed tracing. |

```csharp
var agent = await new AgentBuilder()
    .WithAzureAI(
        endpoint: "https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT",
        model: "YOUR-DEPLOYMENT-NAME",
        configure: azure =>
        {
            azure.AuthMode = AzureAIAuthMode.DefaultAzureCredential;
            azure.ProjectServiceVersion = AzureAIProjectServiceVersion.V1;
            azure.OpenAIServiceVersion = AzureAIOpenAIServiceVersion.V2025_04_01_Preview;
            azure.OpenAIConnectionId = "Azure.AI.OpenAI.AzureOpenAIClient";
            azure.UserAgentApplicationId = "hpdos";
            azure.NetworkTimeoutMs = 120000;
            azure.EnableDistributedTracing = true;
        })
    .BuildAsync();
```

Stored config uses the same option names:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "azure-ai",
      "Endpoint": "https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT",
      "ModelName": "YOUR-DEPLOYMENT-NAME",
      "ProviderOptions": {
        "authMode": "DefaultAzureCredential",
        "projectServiceVersion": "V1",
        "openAIServiceVersion": "V2025_04_01_Preview",
        "openAIConnectionId": "Azure.AI.OpenAI.AzureOpenAIClient",
        "userAgentApplicationId": "hpdos",
        "networkTimeoutMs": 120000,
        "enableDistributedTracing": true
      }
    }
  }
}
```

## Direct Azure OpenAI-Compatible Endpoints

`azure-ai` can also create a chat client for a direct Azure OpenAI-compatible endpoint:

```bash
export AZURE_AI_ENDPOINT="https://YOUR-RESOURCE.openai.azure.com/"
export AZURE_AI_API_KEY="..."
```

```csharp
var agent = await new AgentBuilder()
    .WithAzureAI(
        endpoint: "https://YOUR-RESOURCE.openai.azure.com/",
        model: "YOUR-DEPLOYMENT-NAME",
        configure: azure =>
        {
            azure.AuthMode = AzureAIAuthMode.Auto;
        })
    .BuildAsync();
```

Use `azure-openai` when you want the traditional Azure OpenAI provider key and the OpenAI provider package's image, embeddings, and hosted-file family support. Use `azure-ai` when your setup starts from an Azure AI Projects / Foundry endpoint.
