# Together AI

The Together AI provider uses provider key `together` and the `HPD-Agent.Providers.Together` package. `ModelName` is a Together chat model id for chat, or a Together embedding model id for embeddings.

The provider package is currently `net10.0` only because the underlying Together SDK targets `net10.0`.

Set an API key:

```bash
export TOGETHER_API_KEY="..."
```

The default endpoint is `https://api.together.ai/v1`. Pass `endpoint` to `WithTogether(...)` / `WithTogetherEmbeddings(...)`, or set `Endpoint` in `ClientProviderConfig`, when you need a compatible proxy or custom Together base URL.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Together;

var agent = await new AgentBuilder()
    .WithTogether(model: "meta-llama/Llama-3.3-70B-Instruct-Turbo")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Together AI setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "together",
      "ModelName": "meta-llama/Llama-3.3-70B-Instruct-Turbo"
    }
  }
}
```

## Embeddings

Together also registers the embeddings provider family under the same `together` key:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Together;

var agent = await new AgentBuilder()
    .WithTogetherEmbeddings(model: "BAAI/bge-large-en-v1.5")
    .BuildAsync();
```

The equivalent family config is:

```json
{
  "Clients": {
    "Embeddings": {
      "ProviderKey": "together",
      "ModelName": "BAAI/bge-large-en-v1.5"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, top-k, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

The Together SDK adapter maps the common Microsoft.Extensions.AI chat options to the Together request. HPD.Agent also maps generic frequency penalty, presence penalty, and `Reasoning` into Together's raw request fields so HPDOS/model metadata can keep using provider-neutral chat config.

Use `TogetherChatRequestOptions` only for Together-specific request fields:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Together;

var agent = await new AgentBuilder()
    .WithTogether(model: "deepseek-ai/DeepSeek-R1")
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        };
    })
    .WithTogetherChatRequestOptions(options =>
    {
        options.ContextLengthExceededBehavior = TogetherContextLengthExceededBehavior.Truncate;
        options.RepetitionPenalty = 1.1;
        options.Logprobs = 5;
        options.MinP = 0.05f;
        options.SafetyModel = "safety_model_name";
    })
    .BuildAsync();
```

The typed Together request options are serializable and can also be applied to a run/session chat config:

```csharp
var runConfig = new AgentRunConfig
{
    Chat = new ChatRunConfig
    {
        Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.Medium
        }
    }
};

runConfig.Chat.UseTogetherChatRequestOptions(new TogetherChatRequestOptions
{
    ContextLengthExceededBehavior = TogetherContextLengthExceededBehavior.Error,
    ReasoningEnabled = true
});
```

Available Together request options:

| Option | Request field | Use |
| --- | --- | --- |
| `ContextLengthExceededBehavior` | `context_length_exceeded_behavior` | Chooses `error` or `truncate` when requested tokens exceed model context. |
| `RepetitionPenalty` | `repetition_penalty` | Penalizes repeated sequences. |
| `Logprobs` | `logprobs` | Returns top token log probabilities for each generation step. |
| `Echo` | `echo` | Includes the prompt in the response. |
| `N` | `n` | Requests multiple completions for each prompt. |
| `MinP` | `min_p` | Uses minimum probability sampling as an alternative to top-p/top-k. |
| `LogitBias` | `logit_bias` | Biases specific token ids. |
| `Compliance` | `compliance` | Passes a Together compliance mode value. |
| `ChatTemplateKwargs` | `chat_template_kwargs` | Sends additional model-engine chat template settings. |
| `SafetyModel` | `safety_model` | Selects a Together moderation model. |
| `ReasoningEnabled` | `reasoning.enabled` | Overrides generic reasoning enable/disable behavior. |

`TogetherProviderConfig` exposes Together-specific embedding defaults, such as `EmbeddingModelId`, for the embeddings family.

For embeddings, `WithTogetherEmbeddings(...)` stores the embedding model in the embeddings family config.

## Caveats

Together chat and embeddings are available only when the `HPD-Agent.Providers.Together` package is referenced by a `net10.0` application.

Together chat supports token streaming, function tools, reasoning content, and JSON object responses through the Microsoft.Extensions.AI adapter. Vision, image generation, rerank, and audio families are not registered by this provider.

Validation checks API key, model, endpoint, and embedding model options. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
