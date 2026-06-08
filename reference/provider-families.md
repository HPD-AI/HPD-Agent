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
| `google-ai` | Chat | Chat provider package. |
| `huggingface` | Chat | Chat provider package. |
| `mistral` | Chat | Chat provider package. |
| `bedrock` | Chat | Uses AWS SDK credential behavior. |
| `ollama` | Chat | Local/server-backed chat provider. |

Do not assume a provider key supports every family. Choose the config slot for the client you need, then use a provider key that is registered for that family.
