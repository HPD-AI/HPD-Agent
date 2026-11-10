# Audio Runtime Attachment

The audio runtime attachment is the bridge between agent turns and audio behavior. It decides how finite audio input is handled, how assistant text is synthesized, where artifacts go, and whether committed transcripts are projected into thread history.

## Attach The Runtime

Use the default attachment when you want the configured audio behavior without extra code:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
```

```csharp
var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithAudio()
    .BuildAsync();
```

Use `WithAudio(...)` for the portable input/output policy and
`WithAudioRuntimeAttachment(...)` when the application needs low-level runtime or sink
configuration:

```csharp
var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithAudio(audio =>
    {
        audio.InputMode = AudioInputMode.BatchSpeechToText;
        audio.OutputMode = AudioOutputMode.TextToSpeech;
        audio.AssistantOutputMode = AssistantOutputSynthesisMode.FinalText;
    })
    .BuildAsync();
```

There are also `WithAudioRuntimeAttachment(...)` overloads for passing low-level attachment options, a thread projection sink, or a session store. Use the session-store overload when committed transcript and assistant-output projections should be written into durable session history.

## Configuration Precedence

Runtime options are compiled in layers:

| Layer | Purpose |
| --- | --- |
| `AudioRuntimeAttachmentOptions` | Low-level runtime, provider route, projection, sink, and output controls. |
| `AgentConfig.Audio` | Portable agent-level input/output modes and policy. |
| `AgentRunConfig.Audio` | Per-run overrides for the portable audio policy. |

The compiler applies low-level options, then `AgentConfig.Audio`, then
`AgentRunConfig.Audio`. When the same portable behavior is configured in more than
one place, the run config is the most specific layer.

## Input Modes

| Mode | What it means |
| --- | --- |
| `Auto` | Use the attachment default policy. |
| `None` | Do not run split finite-audio input handling. |
| `BatchSpeechToText` | Transcribe finite input audio and inject transcript text. |
| `ReferenceOnly` | Keep audio as reference content without batch transcription. |
| `Reject` | Reject audio input for this agent or run. |
| `ProviderRealtime` | Leave finite-audio handling alone for native realtime provider transport. |
| `StreamingSpeechToText` | Use the retained streaming STT path for an Audio session transport. |

Use `BatchSpeechToText` for uploaded audio files. Use `ProviderRealtime` with [Realtime Audio](realtime-audio.md), where the realtime model path owns the audio interaction.

Input detection is content-based:

- text-only user messages do not run finite audio input handling,
- audio-containing messages are detected before content upload,
- mixed text-and-audio messages keep the original text,
- committed transcripts are added as additional text content when transcript projection into the user message is enabled,
- `ReferenceOnly` keeps media identity available without batch transcription.

## Output Modes

| Mode | What it means |
| --- | --- |
| `Auto` | Use the attachment default policy. |
| `None` | Do not synthesize assistant audio. |
| `TextOnly` | Keep assistant text only. |
| `TextToSpeech` | Synthesize assistant text through the TTS path. |
| `ProviderRealtimeAudio` | Leave assistant audio to the native realtime provider path. |

The most common non-realtime output mode is final-text TTS: the model produces text, then the runtime synthesizes that text.

## Thread Projection

The runtime can project committed transcripts and assistant output into thread history. By default, the useful durable representation is text:

- user audio input becomes transcript text,
- assistant text remains the source of truth,
- assistant audio artifacts are stored separately through `IContentStore`.

Content uploads and audio artifacts are thread-scoped. Normal `RunAsync(..., sessionId: ...)` execution supplies the active thread context for you. When you construct event input directly, include both `SessionId` and `ThreadId` so upload, artifact, and thread-projection middleware can use the same durable scope. See [Content Upload And Resolution](../content/content-upload-and-resolution.md) for the generic upload and resolver flow.

Be deliberate before storing raw audio in durable history. Audio retention usually has stricter product and privacy requirements than text transcript retention.

## Active Boundaries

Some policy objects contain lower-level privacy, trace, and projection flags. Prefer documenting behavior you have wired and tested in your app rather than exposing every internal knob. The stable user-facing model is: text is durable by default, raw media is explicit, and realtime provider transport is distinct from finite STT/TTS runtime work.

## Related Reading

- [Speech To Text Input](speech-to-text-input.md)
- [Text To Speech Output](text-to-speech-output.md)
- [Realtime Audio](realtime-audio.md)
- [Audio Events And Traces](audio-events-and-traces.md)
