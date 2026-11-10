# Hugging Face

The Hugging Face provider uses provider key `huggingface` and the `HPD-Agent.Providers.HuggingFace` package. `ModelName` is a Hugging Face repository id, such as `meta-llama/Meta-Llama-3-8B-Instruct`.

The provider package is currently `net10.0` only because the underlying Hugging Face SDK targets `net10.0`.

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
  "clients": {
    "chat": {
      "provider": {
        "key": "huggingface"
      },
      "modelName": "meta-llama/Meta-Llama-3-8B-Instruct"
    }
  }
}
```

The default endpoint is `https://router.huggingface.co/`. Pass `endpoint` to `WithHuggingFace(...)`, or set `Endpoint` in `ChatClientConfig`, when you need a compatible proxy or custom Hugging Face router URL.

## Runtime Chat Options

Use `AgentRunConfig.Clients.Chat.Provider.Key` and `ModelName` for selection. Use `ChatClientConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, seed, frequency penalty, presence penalty, response format, and tools. Put agent-level defaults in `AgentConfig.Clients.Chat`; use `AgentRunConfig.Clients.Chat` for complete per-run replacements.

Use `HuggingFaceChatRequestOptions` only for Hugging Face-specific request fields:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.HuggingFace;

var agent = await new AgentBuilder()
    .WithHuggingFace(model: "meta-llama/Meta-Llama-3-8B-Instruct")

    .WithHuggingFaceChatRequestOptions(options =>
    {
        options.Logprobs = true;
        options.TopLogprobs = 5;
        options.ToolPrompt = "Return tool calls as JSON.";
    })
    .BuildAsync();
```

The typed Hugging Face request options are serializable and can also be applied to a run/session chat config:

```csharp
var runConfig = new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new HPD.Agent.Providers.ProviderReference { Key = "huggingface" },
            ModelName = "mistralai/Mistral-7B-Instruct-v0.2",
            MaxOutputTokens = 512
        }
    }
};

runConfig.Clients.Chat!.UseHuggingFaceChatRequestOptions(new HuggingFaceChatRequestOptions
{
    Logprobs = true,
    TopLogprobs = 3
});
```

Available Hugging Face request options:

| Option | Request field | Use |
| --- | --- | --- |
| `Logprobs` | `logprobs` | Includes output token log probabilities. |
| `TopLogprobs` | `top_logprobs` | Returns the most likely tokens at each generated token position. |
| `N` | `n` | Requests multiple chat completion choices. |
| `LogitBias` | `logit_bias` | Passes raw token-bias values supported by the SDK request shape. |
| `ToolPrompt` | `tool_prompt` | Adds tool-use steering text before tool definitions. |

## Caveats

Hugging Face chat is available only when the `HPD-Agent.Providers.HuggingFace` package is referenced by a `net10.0` application.

Runtime validation checks repository id and endpoint. Credentials are resolved lazily when a run first acquires this provider client; a missing credential fails that run, not `BuildAsync()`. Provider metadata reports no vision support. Tool support depends on the selected Hugging Face model and endpoint behavior.
