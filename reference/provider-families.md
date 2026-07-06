# Provider Families

A provider key names a provider registration. A provider family names the kind of client HPD Agent asks that provider to create.

The same key can support more than one family. For example, `openai` can resolve chat clients from the OpenAI provider package and audio clients from the OpenAI audio provider package when those packages are referenced. A key can also support only one family. `onnx-runtime` is a chat provider key; it does not provide speech, image, embedding, or hosted-file clients.

## Families

HPD Agent uses family-specific provider contracts:

| Family | Contract | Common config slot |
| --- | --- | --- |
| Chat | `IChatClientProvider` | `Clients.Chat` |
| Text to speech | `ITextToSpeechClientProvider` | `Clients.TextToSpeech` |
| Speech to text | `ISpeechToTextClientProvider` | `Clients.SpeechToText` |
| Realtime | `IRealtimeClientProvider` | `Clients.Realtime` |
| Image generation | `IImageGenerationClientProvider` | `Clients.ImageGeneration` |
| Embeddings | `IEmbeddingGeneratorProvider` | `Clients.Embeddings` |
| Hosted files | `IHostedFileClientProvider` | `Clients.HostedFiles` |
| Voice activity detection | `IVoiceActivityDetectorProvider` | family-specific runtime options |
| End-of-turn detection | `IEndOfTurnDetectorProvider` | family-specific runtime options |

## Resolve A Family

Provider resolution is typed. A key must exist for the requested family:

```csharp
using HPD.Agent.Providers;

IProviderRegistry registry = new ProviderRegistry();

var chatProvider =
    registry.GetRequiredProvider<IChatClientProvider>("openai");

var ttsProvider =
    registry.GetRequiredProvider<ITextToSpeechClientProvider>("openai");
```

Both calls require the relevant provider implementations to be registered. If `openai` is registered only for chat, the text-to-speech lookup fails with a family-specific provider error.

Application code usually does not create a bare registry. `AgentBuilder` loads provider modules from referenced provider assemblies and exposes the populated registry through `builder.ProviderRegistry`.

## Composite Providers

When multiple provider implementations use the same key for different families, HPD Agent combines them into a composite provider. The composite lets `Clients.Chat`, `Clients.TextToSpeech`, `Clients.SpeechToText`, and other slots use the same provider key while still resolving different client contracts.

That is why this configuration is meaningful when the corresponding provider packages are referenced:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "openai",
      "ModelName": "gpt-5-mini"
    },
    "TextToSpeech": {
      "ProviderKey": "openai",
      "ModelName": "gpt-4o-mini-tts"
    }
  }
}
```

The key is shared. The family is selected by the config slot.

## Known Provider Family Boundaries

| Provider key | Source-confirmed families | Notes |
| --- | --- | --- |
| `openai` | Chat, image generation, embeddings, hosted files, text to speech, speech to text, realtime | Chat/image/embedding/hosted-file families require the OpenAI provider package. Audio families require the OpenAI audio provider package. See [OpenAI And Azure OpenAI](../guides/providers/openai-and-azure-openai.md) and [OpenAI Audio](../guides/providers/openai-audio.md). |
| `azure-openai` | Chat, image generation, embeddings, hosted files | Traditional Azure OpenAI resource path. Model names are deployment names. See [OpenAI And Azure OpenAI](../guides/providers/openai-and-azure-openai.md). |
| `azure-ai` | Chat | Azure AI Projects / Foundry path with OAuth-sensitive endpoint behavior. |
| `elevenlabs` | Speech to text, text to speech | Realtime Scribe uses the speech-to-text streaming path; it is not the `Realtime` provider family. See [ElevenLabs Audio](../guides/providers/elevenlabs-audio.md). |
| `onnx-runtime` | Chat | Requires an existing local model path. Compatible ONNX Runtime GenAI instruct models can opt into structured tool calling. See [ONNX Runtime](../guides/providers/onnx-runtime.md). |
| `anthropic` | Chat | Chat provider package. |
| `cohere` | Chat, embeddings | Net10-only provider package. Chat streaming currently yields a single final update. See [Cohere](../guides/providers/cohere.md). |
| `dashscope` | Chat, embeddings | Net10-only provider package. Chat supports streaming, function tools, reasoning content, and multimodal models through the Cnblogs DashScope adapter. See [DashScope](../guides/providers/dashscope.md). |
| `cerebras` | Chat | Provider package. Chat uses Cerebras' OpenAI-compatible chat completions API. See [Cerebras](../guides/providers/cerebras.md). |
| `deepseek` | Chat | Provider package. Chat uses DeepSeek's OpenAI-compatible chat completions API. See [DeepSeek](../guides/providers/deepseek.md). |
| `deepinfra` | Chat | Chat provider package backed by the shared OpenAI-compatible chat-completions client. See [DeepInfra](../guides/providers/deepinfra.md). |
| `fireworks` | Chat | Net10-only provider package. Chat uses the shared OpenAI-compatible chat-completions base. See [Fireworks AI](../guides/providers/fireworks.md). |
| `sambanova` | Chat | Provider package. Chat uses SambaNova's OpenAI-compatible chat completions API. See [SambaNova](../guides/providers/sambanova.md). |
| `hyperbolic` | Chat | Provider package. Chat uses Hyperbolic's OpenAI-compatible chat completions API. See [Hyperbolic](../guides/providers/hyperbolic.md). |
| `ovhcloud` | Chat | Provider package. Chat uses OVHcloud AI Endpoints' OpenAI-compatible chat completions API. See [OVHcloud AI Endpoints](../guides/providers/ovhcloud.md). |
| `nscale` | Chat | Provider package. Chat uses Nscale's OpenAI-compatible chat completions API. See [Nscale](../guides/providers/nscale.md). |
| `venice` | Chat | Provider package. Chat uses Venice.ai's OpenAI-compatible chat completions API. See [Venice.ai](../guides/providers/venice.md). |
| `perplexity` | Chat | Provider package. Chat uses Perplexity's OpenAI-compatible Sonar API. See [Perplexity](../guides/providers/perplexity.md). |
| `lmstudio` | Chat | Local provider package. Chat uses LM Studio's OpenAI-compatible local server and does not require an API key by default. See [LM Studio](../guides/providers/lmstudio.md). |
| `nebius` | Chat | Provider package. Chat uses Nebius Token Factory's OpenAI-compatible API. See [Nebius Token Factory](../guides/providers/nebius.md). |
| `nvidia-nim` | Chat | Provider package. Chat uses NVIDIA NIM's OpenAI-compatible API. See [NVIDIA NIM](../guides/providers/nvidia-nim.md). |
| `siliconflow` | Chat | Provider package. Chat uses SiliconFlow's OpenAI-compatible API. See [SiliconFlow](../guides/providers/siliconflow.md). |
| `scaleway` | Chat | Provider package. Chat uses Scaleway Generative APIs' OpenAI-compatible API. See [Scaleway Generative APIs](../guides/providers/scaleway.md). |
| `zai` | Chat | Provider package. Chat uses Z.AI's OpenAI-compatible API. See [Z.AI](../guides/providers/zai.md). |
| `minimax` | Chat | Provider package. Chat uses MiniMax's OpenAI-compatible API. See [MiniMax](../guides/providers/minimax.md). |
| `together` | Chat, embeddings | Net10-only provider package. Chat supports token streaming, function tools, reasoning content, and JSON object responses. See [Together AI](../guides/providers/together.md). |
| `xai` | Chat | Chat provider package backed by the shared OpenAI-compatible chat-completions client. Images, embeddings, audio, files, batches, and deferred completions are not registered yet. See [xAI](../guides/providers/xai.md). |
| `groq` | Chat | Net10-only provider package. Chat uses Groq's OpenAI-compatible chat completions API and supports token streaming, function tools, and JSON object responses. See [Groq](../guides/providers/groq.md). |
| `moonshot` | Chat | Net10-only provider package. Chat uses Moonshot/Kimi's OpenAI-compatible chat completions API and supports token streaming, function tools, JSON object responses, seed, and optional Kimi thinking fields. See [Moonshot](../guides/providers/moonshot.md). |
| `replicate` | Image generation | Net10-only provider package. Exposes bounded Replicate model predictions that return image URLs through `IImageGenerator`; it does not register a general prediction/model-run family. See [Replicate](../guides/providers/replicate.md). |
| `google-ai` | Chat | Chat provider package. |
| `huggingface` | Chat | Chat provider package. |
| `mistral` | Chat | Chat provider package. |
| `bedrock` | Chat | Uses AWS SDK credential behavior. |
| `ollama` | Chat | Local/server-backed chat provider. |

Do not assume a provider key supports every family. Choose the config slot for the client you need, then use a provider key that is registered for that family.
