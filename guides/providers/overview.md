# Provider Setup Overview

Provider setup has one shape:

1. Install the provider package.
2. Let the source generator include its provider manifest in the application.
3. For DI hosts, call `AddHpdGeneratedProviders()`.
4. Configure an optional agent default or provide `Clients.*` per run.
5. Put secrets in environment variables or an explicit secret resolver.
6. Run the agent with `RunAsync(...)`.

## Provider Capability Snapshot

Provider keys do not all mean the same thing. Some keys only create chat clients; some keys create multiple client families; some features such as hosted files or native realtime require a specific family slot.

| Provider key | Chat | STT | TTS | Realtime | Images | Embeddings | Hosted files | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `openai` | yes | yes | yes | yes | yes | yes | yes | Chat and non-audio families come from `HPD-Agent.Providers.OpenAI`; audio families come from the OpenAI audio provider package. |
| `azure-openai` | yes | no | no | no | yes | yes | yes | Traditional Azure OpenAI resource path; `ModelName` is a deployment name. |
| `azure-ai` | yes | no | no | no | no | no | no | Azure AI Projects / Foundry chat path; endpoint/auth requirements differ from `azure-openai`. |
| `anthropic` | yes | no | no | no | no | no | no | Chat provider package. |
| `cohere` | yes | no | no | no | no | yes | no | Net10-only chat and embeddings provider package. Streaming produces a single final update. |
| `dashscope` | yes | no | no | no | no | yes | no | Net10-only chat and embeddings provider package backed by the Cnblogs DashScope MEAI adapter. |
| `cerebras` | yes | no | no | no | no | no | no | Chat provider package backed by Cerebras' OpenAI-compatible chat completions API. |
| `deepseek` | yes | no | no | no | no | no | no | Chat provider package backed by DeepSeek's OpenAI-compatible chat completions API. |
| `deepinfra` | yes | no | no | no | no | no | no | Chat provider package backed by the shared OpenAI-compatible chat-completions client. |
| `fireworks` | yes | no | no | no | no | no | no | Net10-only chat provider package backed by the shared OpenAI-compatible chat-completions base. |
| `sambanova` | yes | no | no | no | no | no | no | Chat provider package backed by SambaNova's OpenAI-compatible chat completions API. |
| `hyperbolic` | yes | no | no | no | no | no | no | Chat provider package backed by Hyperbolic's OpenAI-compatible chat completions API. |
| `ovhcloud` | yes | no | no | no | no | no | no | Chat provider package backed by OVHcloud AI Endpoints' OpenAI-compatible chat completions API. |
| `nscale` | yes | no | no | no | no | no | no | Chat provider package backed by Nscale's OpenAI-compatible chat completions API. |
| `venice` | yes | no | no | no | no | no | no | Chat provider package backed by Venice.ai's OpenAI-compatible chat completions API. |
| `perplexity` | yes | no | no | no | no | no | no | Chat provider package backed by Perplexity's OpenAI-compatible Sonar API. |
| `lmstudio` | yes | no | no | no | no | no | no | Local chat provider package backed by LM Studio's OpenAI-compatible local server. |
| `nebius` | yes | no | no | no | no | no | no | Chat provider package backed by Nebius Token Factory's OpenAI-compatible API. |
| `nvidia-nim` | yes | no | no | no | no | no | no | Chat provider package backed by NVIDIA NIM's OpenAI-compatible API. |
| `siliconflow` | yes | no | no | no | no | no | no | Chat provider package backed by SiliconFlow's OpenAI-compatible API. |
| `scaleway` | yes | no | no | no | no | no | no | Chat provider package backed by Scaleway Generative APIs' OpenAI-compatible API. |
| `zai` | yes | no | no | no | no | no | no | Chat provider package backed by Z.AI's OpenAI-compatible API. |
| `minimax` | yes | no | no | no | no | no | no | Chat provider package backed by MiniMax's OpenAI-compatible API. |
| `together` | yes | no | no | no | no | yes | no | Net10-only chat and embeddings provider package. Supports token streaming, function tools, reasoning content, and JSON object responses. |
| `xai` | yes | no | no | no | no | no | no | Chat provider package backed by the shared OpenAI-compatible chat-completions client. Images, embeddings, audio, files, batches, and deferred completions are deferred. |
| `groq` | yes | no | no | no | no | no | no | Net10-only chat provider package backed by Groq's OpenAI-compatible chat completions API. |
| `moonshot` | yes | no | no | no | no | no | no | Net10-only chat provider package backed by Moonshot/Kimi's OpenAI-compatible chat completions API. |
| `replicate` | no | no | no | no | yes | no | no | Net10-only image generation provider package backed by bounded Replicate model predictions. |
| `google-ai` | yes | no | no | no | no | no | no | Chat provider package. |
| `ollama` | yes | no | no | no | no | no | no | Local or server-backed chat; no API key required. |
| `huggingface` | yes | no | no | no | no | no | no | Chat provider package. |
| `mistral` | yes | no | no | no | no | no | no | Chat provider package. |
| `bedrock` | yes | no | no | no | no | no | no | Uses AWS SDK credential behavior. |
| `onnx-runtime` | yes | no | no | no | no | no | no | Local ONNX Runtime GenAI chat provider. |
| `elevenlabs` | no | yes | yes | no | no | no | no | Realtime Scribe is exposed through the speech-to-text family, not `Clients.Realtime`. |

Use this table to choose the provider family slot. Then open the provider-specific page for package names, environment variables, and typed options.

Fluent setup should be the first choice in application code:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Anthropic;

var agent = await new AgentBuilder()
    .WithAnthropic(model: "claude-3-5-sonnet-latest")
    .BuildAsync();

var result = await agent.RunAsync("Summarize provider setup in one sentence.");
Console.WriteLine(result.Text);
```

JSON or configuration setup is useful when agent definitions are stored outside code. Use `Clients.Chat`:

```json
{
  "clients": {
    "chat": {
      "provider": {
        "key": "anthropic"
      },
      "modelName": "claude-3-5-sonnet-latest"
    }
  }
}
```

Provider-specific construction and transport options live on the same family config through `ProviderConfig`. For example, audio providers use provider configuration for voice, output format, speech speed, transcript options, and realtime provider ids. Chat providers use provider configuration only for controls that are native to that provider or SDK.

Provider and model selection uses `AgentRunConfig.Clients.Chat.Provider.Key` and `ModelName`. Runtime chat behavior uses `ChatClientConfig`: temperature, top-p, max output tokens, stop sequences, response format, tools, tool mode, seed, and generic reasoning options. Agent-level defaults belong in `AgentConfig.Clients.Chat`; per-run replacements belong in `AgentRunConfig.Clients.Chat`.

In C# code, use the provider's typed config with `SetProviderConfig(...)` when available. In stored JSON, use the `ProviderConfig` object.

## Fluent Helpers And Family Config

Most application code should use provider builder extensions because they keep setup short and strongly typed:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Audio.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAITextToSpeech(model: "tts-1", voice: "nova")
    .BuildAsync();
```

Those helpers do not create a different system. They populate the same `Clients.*` family config that JSON uses:

| Setup style | Best for | Tradeoff |
| --- | --- | --- |
| Fluent builder extension | Normal C# apps, samples, tests, local setup. | Requires referencing the provider package in code. |
| `Clients.*` JSON/config | Stored agent definitions, FFI, generated configs, control planes. | More verbose; provider configurations are stored in `ProviderConfig`. |
| Manual `ChatClientConfig` + `SetProviderConfig(...)` | Advanced C# composition or dynamic family wiring. | More ceremony, but exact control over the family slot. |

For audio, provider setup and runtime behavior are separate. `.WithOpenAITextToSpeech(...)` chooses a TTS provider; `WithAudioRuntimeAttachment(...)` or `WithAudio()` decides when assistant text is synthesized, where artifacts go, and whether playback/projection is enabled.

## Provider Keys

Use these current chat provider keys:

| Provider | Key | Primary setup page |
| --- | --- | --- |
| OpenAI | `openai` | [OpenAI And Azure OpenAI](openai-and-azure-openai.md) |
| Azure OpenAI | `azure-openai` | [OpenAI And Azure OpenAI](openai-and-azure-openai.md) |
| Azure AI | `azure-ai` | [Azure AI](azure-ai.md) |
| Anthropic | `anthropic` | [Anthropic](anthropic.md) |
| Cohere | `cohere` | [Cohere](cohere.md) |
| DashScope | `dashscope` | [DashScope](dashscope.md) |
| Cerebras | `cerebras` | [Cerebras](cerebras.md) |
| DeepSeek | `deepseek` | [DeepSeek](deepseek.md) |
| DeepInfra | `deepinfra` | [DeepInfra](deepinfra.md) |
| Fireworks AI | `fireworks` | [Fireworks AI](fireworks.md) |
| SambaNova | `sambanova` | [SambaNova](sambanova.md) |
| Hyperbolic | `hyperbolic` | [Hyperbolic](hyperbolic.md) |
| OVHcloud AI Endpoints | `ovhcloud` | [OVHcloud AI Endpoints](ovhcloud.md) |
| Nscale | `nscale` | [Nscale](nscale.md) |
| Venice.ai | `venice` | [Venice.ai](venice.md) |
| Perplexity | `perplexity` | [Perplexity](perplexity.md) |
| LM Studio | `lmstudio` | [LM Studio](lmstudio.md) |
| Nebius Token Factory | `nebius` | [Nebius Token Factory](nebius.md) |
| NVIDIA NIM | `nvidia-nim` | [NVIDIA NIM](nvidia-nim.md) |
| SiliconFlow | `siliconflow` | [SiliconFlow](siliconflow.md) |
| Scaleway Generative APIs | `scaleway` | [Scaleway Generative APIs](scaleway.md) |
| Z.AI | `zai` | [Z.AI](zai.md) |
| MiniMax | `minimax` | [MiniMax](minimax.md) |
| Together AI | `together` | [Together AI](together.md) |
| xAI | `xai` | [xAI](xai.md) |
| Groq | `groq` | [Groq](groq.md) |
| Moonshot | `moonshot` | [Moonshot](moonshot.md) |
| Replicate | `replicate` | [Replicate](replicate.md) |
| Google AI | `google-ai` | [Google AI](google-ai.md) |
| Ollama | `ollama` | [Ollama](ollama.md) |
| Hugging Face | `huggingface` | [Hugging Face](huggingface.md) |
| Mistral | `mistral` | [Mistral](mistral.md) |
| Amazon Bedrock | `bedrock` | [Amazon Bedrock](bedrock.md) |
| ONNX Runtime | `onnx-runtime` | [ONNX Runtime](onnx-runtime.md) |
| OpenAI audio | `openai` | [OpenAI Audio](openai-audio.md) |
| ElevenLabs audio | `elevenlabs` | [ElevenLabs Audio](elevenlabs-audio.md) |

OpenRouter and Azure AI Inference legacy are outside the primary setup path. OpenRouter currently has a provider implementation but no package-level fluent `AgentBuilder` helper. Azure AI Inference is obsolete and should be treated as legacy-only.

## Agent Definition Style

HPD separates provider selection from agent definition.

Use `AgentBuilder` when the app owns the agent definition in code. Use stored agent definitions when a hosted app needs to create, update, list, or select agent configs at runtime. Use an `IAgentFactory` override when construction depends on application services or policy that should not be stored as JSON.

This is different from frameworks where each provider produces a different agent subclass. In HPD, provider packages create family-specific clients; the `Agent` runtime still owns sessions, threads, middleware, events, tools, and hosting behavior.

## Setup Caveats

Agent construction validates provider-independent structure. Provider-specific configuration is validated and credentials are resolved when a run acquires its provider client. Secret resolution can use explicit runtime secrets, environment variables, custom resolvers, or configuration.

| Setup path | Behavior |
| --- | --- |
| OpenAI, Azure OpenAI, Anthropic, Cohere, DashScope, Cerebras, DeepSeek, DeepInfra, Fireworks AI, SambaNova, Hyperbolic, OVHcloud AI Endpoints, Nscale, Venice.ai, Perplexity, Nebius Token Factory, NVIDIA NIM, SiliconFlow, Scaleway Generative APIs, Z.AI, MiniMax, Together AI, xAI, Groq, Moonshot, Google AI, Hugging Face, Mistral | Env-backed fluent and `Clients.Chat` setup can use the documented aliases. Missing credentials fail when a run first acquires that provider client, not during `BuildAsync()`. |
| LM Studio | Local fluent setup works without credentials when the LM Studio local server is running. Endpoint and optional API-key aliases are available for proxied or secured local setups. |
| Bedrock | Region env is accepted; credentials normally flow through the AWS SDK default credential chain. |
| Ollama | Endpoint env aliases apply to fluent `.WithOllama(...)`; JSON/config setup should include `Endpoint` when not using localhost. |
| ONNX Runtime | Local model paths are required for live inference. Compatible ONNX Runtime GenAI instruct models can opt into [structured tool calling](onnx-structured-tool-calling.md). |

See [Providers, Clients, And Secrets](../../concepts/providers-clients-and-secrets.md), [Provider Families](../../reference/provider-families.md), and [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md).

For per-run provider/model switching, named static credentials, serialization rules,
and client-cache behavior, see [Runtime Provider Selection And Authentication](runtime-selection-and-authentication.md).
