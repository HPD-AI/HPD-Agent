# OpenAI Audio Provider

The OpenAI audio provider contributes audio client families under the `openai` provider key.

Use it when an agent needs OpenAI speech-to-text, text-to-speech, or realtime audio clients in addition to ordinary chat setup.

## Package And Families

Reference the OpenAI audio provider package or project. The provider key is:

```text
openai
```

Source-confirmed client families:

| Family | Config slot |
| --- | --- |
| Speech to text | `Clients.SpeechToText` |
| Text to speech | `Clients.TextToSpeech` |
| Realtime | `Clients.Realtime` |

The OpenAI chat provider package and OpenAI audio provider package can share the same provider key because provider resolution is family-specific.

## Secrets

The OpenAI audio provider uses the same common OpenAI aliases:

```text
OPENAI_API_KEY
OPENAI_ENDPOINT
OPENAI_BASE_URL
```

Configuration keys such as `openai:ApiKey` can also be resolved by the provider secret system.

## Text To Speech

Configure text-to-speech through `Clients.TextToSpeech` or the audio runtime attachment options used by your application.

In C# apps, the fluent helper configures the same `Clients.TextToSpeech` family slot:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
using HPD.Agent.Providers.Audio.OpenAI;
```

```csharp
var agent = await new AgentBuilder()
    .WithOpenAITextToSpeech(
        model: "tts-1",
        voice: "nova",
        outputFormat: "mp3",
        configure: options => options.Speed = 1.0f)
    .WithAudioRuntimeAttachment(audio =>
    {
        audio.AssistantOutputSynthesisMode = AssistantOutputSynthesisMode.FinalText;
    })
    .BuildAsync();
```

Source defaults include:

| Option | Default |
| --- | --- |
| Model | `tts-1` |
| Voice | `nova` |
| Format | `mp3` |

The provider metadata also lists newer TTS model ids such as `tts-1-hd` and `gpt-4o-mini-tts`.

Provider-specific TTS options use `OpenAITtsConfig` through `ProviderOptionsJson`:

```json
{
  "Clients": {
    "TextToSpeech": {
      "ProviderKey": "openai",
      "ModelName": "tts-1",
      "ProviderOptionsJson": "{\"defaultVoiceId\":\"nova\",\"outputFormat\":\"mp3\",\"speed\":1.0}"
    }
  }
}
```

Source-confirmed TTS provider options:

| Option | Use it for |
| --- | --- |
| `apiKey` | Provider-specific API key override. |
| `baseUrl` | Provider-specific base URL override. |
| `defaultModelId` | Default TTS model when `ModelName` is not set. |
| `defaultVoiceId` | Default voice id. |
| `outputFormat` | Default output format. |
| `speed` | Speech speed. |

## Speech To Text

Configure speech-to-text through `Clients.SpeechToText`.

In C# apps, use the speech-to-text helper when finite audio input should use OpenAI transcription:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
using HPD.Agent.Providers.Audio.OpenAI;
```

```csharp
var agent = await new AgentBuilder()
    .WithOpenAISpeechToText(
        model: "whisper-1",
        configure: options => options.Prompt = "Technical support call")
    .BuildAsync();
```

The helper only selects the speech-to-text client family. The audio runtime attachment still decides when finite audio input is transcribed and how committed transcripts are projected into the turn.

Source defaults include:

| Option | Default |
| --- | --- |
| Model | `whisper-1` |

The provider metadata also lists `gpt-4o-transcribe` and `gpt-4o-mini-transcribe`.

Finite audio input uses HPD audio runtime wiring; see [Speech To Text Input](../audio/speech-to-text-input.md).

Provider-specific STT options use `OpenAISttConfig` through `ProviderOptionsJson`:

```json
{
  "Clients": {
    "SpeechToText": {
      "ProviderKey": "openai",
      "ModelName": "whisper-1",
      "ProviderOptionsJson": "{\"prompt\":\"Technical support call\",\"responseFormat\":\"json\"}"
    }
  }
}
```

Source-confirmed STT provider options:

| Option | Use it for |
| --- | --- |
| `apiKey` | Provider-specific API key override. |
| `baseUrl` | Provider-specific base URL override. |
| `defaultModelId` | Default STT model when `ModelName` is not set. |
| `prompt` | Provider transcription prompt. |
| `temperature` | Provider transcription temperature. |
| `responseFormat` | Provider response format. |
| `timestampGranularities` | Timestamp granularities. |
| `includeLogprobs` | Include log probabilities when supported. |

## Realtime

OpenAI realtime support is present in source under `Clients.Realtime`. Source defaults include:

In C# apps, the realtime helper configures the realtime family; the run path still needs realtime transport:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Audio.OpenAI;
```

```csharp
var agent = await new AgentBuilder()
    .WithOpenAIRealtime(model: "gpt-realtime")
    .BuildAsync();
```

| Option | Default |
| --- | --- |
| Model | `gpt-realtime` |

Use realtime with `AgentModelTransportMode.Realtime`. The ordinary chat path and finite speech-to-text path are separate from native realtime transport.

Provider-specific realtime options use `OpenAIRealtimeConfig` through `ProviderOptionsJson`:

```json
{
  "Clients": {
    "Realtime": {
      "ProviderKey": "openai",
      "ModelName": "gpt-realtime",
      "ProviderOptionsJson": "{\"organizationId\":\"org_...\",\"projectId\":\"proj_...\"}"
    }
  }
}
```

Source-confirmed realtime provider options:

| Option | Use it for |
| --- | --- |
| `apiKey` | Provider-specific API key override. |
| `baseUrl` | Provider-specific base URL override. |
| `defaultModelId` | Default realtime model when `ModelName` is not set. |
| `organizationId` | OpenAI organization id. |
| `projectId` | OpenAI project id. |

## C# Provider Options

The fluent helpers are the shortest path. Manual typed config is still useful when you are dynamically composing family configs or loading part of the setup outside normal builder code:

```csharp
using HPD.Agent;
using HPD.Agent.Providers;
using HPD.Agent.Providers.Audio.OpenAI;
```

```csharp
var tts = new ClientProviderConfig
{
    ProviderKey = "openai",
    ModelName = "tts-1"
};

tts.SetProviderConfig(
    new OpenAITtsConfig
    {
        DefaultVoiceId = "nova",
        OutputFormat = "mp3",
        Speed = 1.0f
    },
    ProviderClientFamily.TextToSpeech);
```

## Live Verification

Credentialed text-to-speech can be verified with the live smoke test:

```bash
HPD_AUDIO_LIVE_SMOKE=1 OPENAI_API_KEY=... \
dotnet test test/HPD.Agent.Audio.V2.Tests/HPD.Agent.Audio.V2.Tests.csproj \
  --framework net10.0 \
  --filter "FullyQualifiedName~OpenAIAudioProviderTests.LiveTextToSpeech_WithConfiguredApiKey_ReturnsAudio"
```

Credentialed realtime agent turns can be verified through the public realtime run path:

```bash
HPD_REALTIME_LIVE_SMOKE=1 OPENAI_API_KEY=... \
dotnet test test/HPD.Agent.Audio.V2.Tests/HPD.Agent.Audio.V2.Tests.csproj \
  --framework net10.0 \
  --filter "FullyQualifiedName~OpenAIAudioProviderTests.LiveRealtimeAgentTurn_WithConfiguredApiKey_ReturnsText"
```

## Related Reading

- [Audio Overview](../audio/overview.md)
- [Audio Runtime Attachment](../audio/runtime-attachment.md)
- [Text To Speech Output](../audio/text-to-speech-output.md)
- [Speech To Text Input](../audio/speech-to-text-input.md)
- [Realtime Audio](../audio/realtime-audio.md)
- [Audio Events And Traces](../audio/audio-events-and-traces.md)
- [Provider Families](../../reference/provider-families.md)
