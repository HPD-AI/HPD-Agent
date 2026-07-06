# Mistral

The Mistral provider uses provider key `mistral` and the `HPD-Agent.Providers.Mistral` package. `ModelName` is a Mistral model id. This provider targets `net10.0`.

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

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, max output tokens, stop sequences, seed, response format, tools, tool mode, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

Mistral reasoning models use the generic reasoning setting:

```csharp
var agent = await new AgentBuilder()
    .WithMistral(model: "mistral-large-latest")
    .WithChatDefaults(chat =>
    {
        chat.Reasoning = new()
        {
            Effort = ReasoningEffort.High
        };
    })
    .BuildAsync();
```

## Mistral Request Options

Use `MistralChatRequestOptions` for Mistral-specific per-request fields that are not generic chat options.

| Option | Purpose |
| --- | --- |
| `SafePrompt` | Enables Mistral safety prompt injection before the conversation. |
| `PredictionContent` | Sends expected completion content for predictable edits and lower latency. |
| `PromptCacheKey` | Sets Mistral's prompt cache key for reusing prompt prefixes. |
| `CompletionCount` | Requests multiple completions for a single prompt. |

```csharp
var agent = await new AgentBuilder()
    .WithMistral(model: "mistral-large-latest")
    .WithMistralChatRequestOptions(mistral =>
    {
        mistral.SafePrompt = true;
        mistral.PromptCacheKey = "workspace-thread-1";
    })
    .BuildAsync();
```

Per-run configuration uses the same type:

```csharp
var run = new AgentRunConfig
{
    Chat = new ChatRunConfig()
        .UseMistralChatRequestOptions(mistral =>
        {
            mistral.PredictionContent = "expected replacement text";
        })
};
```

## Caveats

Validation checks model configuration. API key resolution happens during provider construction through `MISTRAL_API_KEY`, explicit `apiKey`, or configured secrets. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Provider metadata reports streaming, function calling, vision input, and audio input support.
