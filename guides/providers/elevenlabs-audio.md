# ElevenLabs Audio Provider

The ElevenLabs audio provider contributes speech-to-text and text-to-speech under the `elevenlabs` provider key.

Use it when an agent should transcribe user audio with Scribe or synthesize assistant text through ElevenLabs voices.

## Package And Families

Reference the ElevenLabs audio provider package or project. The provider key is:

```text
elevenlabs
```

Source-confirmed client families:

| Family | Config slot |
| --- | --- |
| Speech to text | `Clients.SpeechToText` |
| Text to speech | `Clients.TextToSpeech` |

ElevenLabs does not provide chat, realtime, image, embedding, or hosted-file clients in this provider package.

## Secrets

Use:

```text
ELEVENLABS_API_KEY
```

Provider-scoped configuration can also use the `elevenlabs:ApiKey` shape.

## Defaults

Source defaults include:

| Option | Default |
| --- | --- |
| STT model | `scribe_v1` |
| Realtime STT model | `scribe_v2_realtime` |
| TTS model | `eleven_turbo_v2_5` |
| TTS voice | `21m00Tcm4TlvDq8ikWAM` |
| TTS output format | `mp3_44100_128` |

## Speech To Text

ElevenLabs speech-to-text uses the Scribe endpoint through the MEAI `ISpeechToTextClient` abstraction.

In C# apps, the fluent helper configures `Clients.SpeechToText`:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Audio.ElevenLabs;
```

```csharp
var agent = await new AgentBuilder()
    .WithElevenLabsSpeechToText(
        model: "scribe_v2",
        language: "en",
        configureOptions: options =>
        {
            options.Diarize = true;
        })
    .BuildAsync();
```

The helper selects the speech-to-text client family. Audio runtime attachment still decides when finite audio input is transcribed and how committed transcripts are projected into the turn.

ElevenLabs also supports realtime speech-to-text through the same `ISpeechToTextClient` streaming method. This is not the `Clients.Realtime` model family and should not be configured as the agent realtime transport. Use it when the app wants live transcript updates from ElevenLabs, then passes committed text into the agent flow.

STT selection uses portable family fields, `ElevenLabsSttConfig` for client
acquisition, and `ElevenLabsSttOptions` for request operations:

```json
{
  "clients": {
    "speechToText": {
      "provider": {
        "key": "elevenlabs"
      },
      "modelName": "scribe_v2",
      "speechLanguage": "en",
      "providerOptions": {
        "diarize": true,
        "timestampsGranularity": "word"
      }
    }
  }
}
```

Source-confirmed STT options:

| Option | Use it for |
| --- | --- |
| `SpeechLanguage` | Portable family field selecting the input/output language. |
| `realtimeModelId` | Default realtime Scribe model, usually `scribe_v2_realtime`. |
| `diarize` | Speaker diarization toggle. |
| `tagAudioEvents` | Audio-event tagging toggle. |
| `timestampsGranularity` | Timestamp granularity, such as `word`. |
| `audioFormat` | Realtime STT audio format, such as `pcm_16000`. |
| `commitStrategy` | Realtime STT commit strategy, such as `manual`. |
| `includeTimestamps` | Include timestamps in realtime transcript responses. |
| `includeLanguageDetection` | Include language detection in realtime transcript responses. |
| `keyterms` | Realtime STT keyterms/hints. |
| `noVerbatim` | Disable verbatim transcript behavior when supported. |
| `vadSilenceThresholdSeconds` | Provider VAD silence threshold. |
| `vadThreshold` | Provider VAD threshold. |
| `minSpeechDurationMilliseconds` | Minimum speech duration for VAD. |
| `minSilenceDurationMilliseconds` | Minimum silence duration for VAD. |
| `enableLogging` | Provider-side logging toggle. |
| `streamingChunkSizeBytes` | Client-side chunk size for streaming audio. |

`ElevenLabsSttConfig.ProviderConfig` contains only `webSocketBaseUrl`. API keys
are selected through `Provider.Authentication`, and HTTP endpoints use the
family `Endpoint` field.

## Text To Speech

ElevenLabs TTS options use `ElevenLabsTtsConfig` through `ProviderConfig`:

In C# apps, the fluent helper configures the same `Clients.TextToSpeech` family slot:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
using HPD.Agent.Providers.Audio.ElevenLabs;
```

```csharp
var agent = await new AgentBuilder()
    .WithElevenLabsTextToSpeech(
        model: "eleven_turbo_v2_5",
        voice: "21m00Tcm4TlvDq8ikWAM",
        outputFormat: "mp3_44100_128",
        configureOptions: options =>
        {
            options.Stability = 0.5;
            options.SimilarityBoost = 0.75;
        })
    .WithAudioRuntimeAttachment(audio =>
    {
        audio.AssistantOutputSynthesisMode = AssistantOutputSynthesisMode.FinalText;
    })
    .BuildAsync();
```

```json
{
  "clients": {
    "textToSpeech": {
      "provider": {
        "key": "elevenlabs"
      },
      "modelName": "eleven_turbo_v2_5",
      "voiceId": "21m00Tcm4TlvDq8ikWAM",
      "audioFormat": "mp3_44100_128",
      "providerOptions": {
        "stability": 0.5,
        "similarityBoost": 0.75
      }
    }
  }
}
```

Source-confirmed TTS options:

| Option | Use it for |
| --- | --- |
| `VoiceId` | Portable family field for the ElevenLabs voice id. |
| `AudioFormat` | Portable family field for output format, such as `mp3_44100_128`. |
| `Speed` | Portable family field for playback speed. |
| `stability` | Voice stability. |
| `similarityBoost` | Voice similarity boost. |
| `style` | Voice style. |
| `useSpeakerBoost` | Speaker boost toggle. |
| `speed` | Speech speed. |
| `applyTextNormalization` | ElevenLabs text-normalization behavior. |
| `enablePushTextStreaming` | Enable push-text streaming support. |
| `pushTextAggregationMode` | Push-text aggregation mode. |
| `autoMode` | Provider auto mode. |
| `syncAlignment` | Alignment synchronization. |
| `inactivityTimeout` | Streaming inactivity timeout. |

`ElevenLabsTtsConfig.ProviderConfig` contains only `webSocketBaseUrl` and
`enablePushTextStreaming`. API keys are selected through
`Provider.Authentication`, and HTTP endpoints use the family `Endpoint` field.

Manual typed config is still useful when you are dynamically composing family configs or loading part of the setup outside normal builder code:

```csharp
using HPD.Agent;
using HPD.Agent.Providers;
using HPD.Agent.Providers.Audio.ElevenLabs;
```

```csharp
var tts = new TextToSpeechClientConfig
{
    Provider = new HPD.Agent.Providers.ProviderReference { Key = "elevenlabs" },
    ModelName = "eleven_turbo_v2_5",
    VoiceId = "21m00Tcm4TlvDq8ikWAM",
    AudioFormat = "mp3_44100_128",
    ProviderConfig = new ElevenLabsTtsConfig(),
    ProviderOptions = new ElevenLabsTtsOptions
    {
        Stability = 0.5,
        SimilarityBoost = 0.75
    }
};
```

## Use With Audio Output

ElevenLabs supplies the TTS client. HPD audio runtime attachment decides when assistant text is synthesized, where audio artifacts are stored, and whether playback hooks are used.

See [Text To Speech Output](../audio/text-to-speech-output.md) for the runtime side of the flow.

## Related Reading

- [Audio Overview](../audio/overview.md)
- [Audio Runtime Attachment](../audio/runtime-attachment.md)
- [Text To Speech Output](../audio/text-to-speech-output.md)
- [Audio Events And Traces](../audio/audio-events-and-traces.md)
- [Provider Families](../../reference/provider-families.md)
