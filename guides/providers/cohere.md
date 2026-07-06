# Cohere

The Cohere provider uses provider key `cohere` and the `HPD-Agent.Providers.Cohere` package. `ModelName` is a Cohere chat model id for chat, or a Cohere embedding model id for embeddings.

The provider package is currently `net10.0` only because the underlying TryAGI-generated Cohere SDK targets `net10.0`.

Set an API key:

```bash
export COHERE_API_KEY="..."
```

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Cohere;

var agent = await new AgentBuilder()
    .WithCohere(model: "command-r-plus")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Cohere setup.");
Console.WriteLine(result.Text);
```

Provider construction stays small. Put model behavior on chat defaults or per-run chat config, and use Cohere request options only for Cohere-only request fields:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Cohere;

var agent = await new AgentBuilder()
    .WithCohere(model: "command-r-plus")
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.TopP = 0.9;
        chat.MaxOutputTokens = 1024;
        chat.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        };
    })
    .WithCohereChatRequestOptions(cohere =>
    {
        cohere.StrictTools = true;
        cohere.CitationMode = CohereCitationMode.Accurate;
        cohere.SafetyMode = CohereSafetyMode.Strict;
        cohere.ThinkingTokenBudget = 2048;
        cohere.Documents =
        [
            new CohereChatDocument
            {
                Id = "setup",
                Data = new Dictionary<string, object>
                {
                    ["title"] = "Setup note",
                    ["body"] = "Runtime model behavior belongs on ChatDefaults."
                }
            }
        ];
    })
    .BuildAsync();
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "cohere",
      "ModelName": "command-r-plus",
      "ChatDefaults": {
        "Temperature": 0.2,
        "TopP": 0.9,
        "MaxOutputTokens": 1024,
        "Reasoning": {
          "Effort": "High"
        },
        "AdditionalProperties": {
          "strict_tools": true,
          "citation_mode": "accurate",
          "safety_mode": "strict",
          "thinking_token_budget": 2048,
          "documents": [
            {
              "id": "setup",
              "data": {
                "title": "Setup note",
                "body": "Runtime model behavior belongs on ChatDefaults."
              }
            }
          ]
        }
      }
    }
  }
}
```

## Embeddings

Cohere also registers the embeddings provider family under the same `cohere` key:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Cohere;

var agent = await new AgentBuilder()
    .WithCohereEmbeddings(model: "embed-english-v3.0")
    .BuildAsync();
```

The equivalent family config is:

```json
{
  "Clients": {
    "Embeddings": {
      "ProviderKey": "cohere",
      "ModelName": "embed-english-v3.0"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, top-k, max output tokens, stop sequences, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

The Cohere adapter maps the common MEAI chat options to Cohere Chat v2 request fields:

| HPD chat option | Cohere request field |
| --- | --- |
| `ModelId` | `model` |
| `Temperature` | `temperature` |
| `TopP` | `p` |
| `TopK` | `k` |
| `MaxOutputTokens` | `max_tokens` |
| `Seed` | `seed` |
| `StopSequences` | `stop_sequences` |
| `ResponseFormat` | `response_format` |
| `Tools` | `tools` |
| `ToolMode` | `tool_choice` |
| `Reasoning` | `thinking` |

Use `CohereChatRequestOptions` for Cohere-only request properties:

| Option | Cohere key | Meaning |
| --- | --- | --- |
| `StrictTools` | `strict_tools` | Forces tool calls to follow their tool definitions strictly. |
| `Documents` | `documents` | Relevant source documents that the model can cite while generating an answer. |
| `CitationMode` | `citation_mode` | Citation generation mode. Supported values include `enabled`, `disabled`, `accurate`, `fast`, and `off`. |
| `SafetyMode` | `safety_mode` | Safety instruction mode. Supported values include `contextual`, `off`, and `strict`. |
| `Logprobs` | `logprobs` | Includes output token log probabilities in the raw Cohere response. |
| `ThinkingEnabled` | `thinking_enabled` | Overrides whether Cohere thinking is enabled. If unset, generic `Reasoning` controls this. |
| `ThinkingTokenBudget` | `thinking_token_budget` | Maximum number of tokens the model can use for thinking. |
| `Priority` | `priority` | Request priority. Lower numbers indicate higher priority; Cohere defaults to `0`. |

`CohereChatDocument` supports:

| Option | Meaning |
| --- | --- |
| `Id` | Optional identifier referenced by Cohere citations. |
| `Text` | Plain document text for simple string documents. |
| `Data` | Structured document data and metadata. |

## Provider Options

`CohereProviderConfig` exposes Cohere-specific embedding defaults for the embeddings family:

| Option | Purpose |
| --- | --- |
| `EmbeddingModelId` | Default embedding model id for the embeddings provider family. |

For embeddings, `WithCohereEmbeddings(...)` stores the embedding model in the embeddings family config.

## Caveats

Cohere chat and embeddings are available only when the `HPD-Agent.Providers.Cohere` package is referenced by a `net10.0` application.

The current TryAGI Cohere MEAI streaming adapter returns a single final update rather than token-by-token streaming.

Validation checks API key, model, endpoint, and embedding model options. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Generic reasoning is mapped to Cohere `thinking`; set `ThinkingEnabled` or `ThinkingTokenBudget` only when you need Cohere-specific control. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
