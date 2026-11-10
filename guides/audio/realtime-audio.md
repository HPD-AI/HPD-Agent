# Realtime Audio

Realtime audio uses a realtime model transport instead of the ordinary chat transport. The provider receives realtime-compatible audio and streams text, audio, transcript, usage, lifecycle, and tool-call updates through the agent turn.

Use realtime audio when the model/provider should own the live audio interaction. Use [Speech To Text Input](speech-to-text-input.md) when the app has finite audio that should be transcribed before a normal model turn.

## Enable Realtime Transport

Realtime is explicit. `Auto` resolves to the normal chat path today, so set the model transport for realtime runs:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
```

```csharp
var result = await agent.RunAsync(
    input,
    runConfig: new AgentRunConfig
    {
        Clients = new AgentClientsConfig
        {
            Transport = AgentModelTransportMode.Realtime,
            Realtime = new RealtimeClientConfig
            {
                Provider = new HPD.Agent.Providers.ProviderReference
                {
                    Key = "openai"
                },
                ModelName = "gpt-realtime"
            }
        }
    });
```

Applications can also provide a realtime client directly for tests or custom transports.

## Send Compatible Audio

Native realtime input accepts realtime-compatible audio such as PCM, PCMU, or PCMA. Convert supported encoded content before sending it to realtime:

```csharp
using HPD.Agent;
using HPD.Agent.Audio;
using Microsoft.Extensions.AI;
```

```csharp
var audio = AudioContent.Wav(wavBytes).ToRealtimeInputAudio();

await agent.RunAsync(new UserMessagesInputEvent
{
    Messages =
    [
        new ChatMessage(ChatRole.User, [audio])
    ],
    RunConfig = new AgentRunConfig
    {
        Clients = new AgentClientsConfig
        {
            Transport = AgentModelTransportMode.Realtime
        }
    }
});
```

Do not assume an arbitrary MP3, OGG, or uploaded browser file can be sent directly into realtime model transport. The finite STT path can handle uploaded formats differently from realtime transport.

Realtime input can still be mixed input. A message may contain typed text and realtime-compatible audio; the text remains part of the turn while the realtime provider owns the audio interaction. By default, realtime transport does not also run the split finite STT runtime. If uploaded audio should be transcribed before a normal chat turn, use [Speech To Text Input](speech-to-text-input.md) instead.

## Realtime Tool Calls

Realtime model turns use the same agent tool execution surface as chat turns. Tool calls emitted by the realtime provider are executed by the agent runtime, wrapped by middleware, persisted as tool call/result content, and submitted back to the realtime session before the next response is requested.

That means existing tool middleware, permissions, function execution context, background work, and custom events still matter in realtime turns.

## Transcripts And Thread History

Realtime can emit user audio transcript deltas, completed transcripts, and failed transcript events. Completed transcript text can be projected back into the user message and thread history. The durable default is still text, not raw audio.

Assistant text from realtime responses is also part of the thread projection path. Provider-owned assistant audio is not the same path as post-turn final-text TTS.

## Managed LiveKit Transport

`HPD-Agent.Audio.LiveKit` provides a managed retained Audio transport. Configure
speech-to-text first, then select the transport:

```csharp
using HPD.Agent.Audio.LiveKit;

var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithOpenAISpeechToText("gpt-4o-mini-transcribe")
    .WithOpenAITextToSpeech("gpt-4o-mini-tts", voice: "alloy")
    .WithLiveKitAudioTransport(
        endpoint: "wss://your-livekit-host",
        credentialResolver: ResolveParticipantCredentialAsync)
    .WithAudio(audio =>
    {
        audio.InputMode = AudioInputMode.StreamingSpeechToText;
        audio.OutputMode = AudioOutputMode.TextToSpeech;
        audio.AssistantOutputMode =
            AssistantOutputSynthesisMode.ProgressiveWithFinalFallback;
        audio.EnablePlayback = true;
    })
    .BuildAsync();
```

The managed LiveKit artifact is qualified for `osx-arm64` in this release. Other
runtime identifiers fail closed with `livekit-runtime-unsupported`. The package
verifies the native artifact before use and resolves room credentials per session;
credentials are not stored in agent configuration.

## Client Boundaries

The hosted ASP.NET `/inputs` route accepts public `AUDIO_SESSION_INPUT` commands
for starting, updating, committing, interrupting, and stopping retained audio
sessions. The browser Web Audio transport in the repository is a private
qualification artifact rather than a published npm package. Applications still
own browser capture, device permissions, WebRTC UI, and transport integration.

## Related Reading

- [Audio Runtime Attachment](runtime-attachment.md)
- [Audio Events And Traces](audio-events-and-traces.md)
- [OpenAI Audio Provider](../providers/openai-audio.md)
- [Tool And Function Events](../events/tool-and-function-events.md)
