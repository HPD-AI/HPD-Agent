# Providers, Clients, And Secrets

Providers connect an HPD agent to model clients. A provider package registers a provider key, such as `openai`, `anthropic`, or `ollama`. The agent configuration chooses a client family, usually `Clients.Chat`, and points that family at a provider key plus a model name.

The same resolution model applies across providers:

1. A fluent `AgentBuilder` helper writes a `ChatClientConfig` into `AgentConfig.Clients.Chat`.
2. `BuildAsync()` records that configuration as the default and creates the secret-resolver chain. It does not create the chat client or resolve its credential.
3. Each `RunAsync(...)` resolves an effective provider, model, endpoint, construction options, request options, and authentication selection.
4. The runtime lazily acquires and leases the matching chat client. Equivalent runs reuse a cached client; different construction identities acquire different clients.

For most applications, prefer fluent setup:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are a concise helpful assistant.")
    .BuildAsync();

var result = await agent.RunAsync("Say hello in one sentence.");
Console.WriteLine(result.Text);
```

Configuration-based setup uses the same model, but writes the client selection directly:

```json
{
  "clients": {
    "chat": {
      "provider": {
        "key": "openai"
      },
      "modelName": "gpt-4o"
    }
  }
}
```

Current configuration belongs under `Clients`, with `Clients.Chat` for the primary chat model.

## Client Families

`AgentConfig.Clients` has separate slots for different model client families:

- `Chat`
- `TextToSpeech`
- `SpeechToText`
- `Realtime`
- `ImageGeneration`
- `Embeddings`
- `HostedFiles`
- `VoiceActivityDetection`
- `EndOfTurnDetection`

Provider setup pages focus first on `Clients.Chat`. Some provider packages expose additional families. See [Provider Families](../reference/provider-families.md) for the broader client-family model.

The family slot matters as much as the provider key. `openai` can mean chat, text-to-speech, speech-to-text, realtime, image generation, embeddings, or hosted files depending on the slot and referenced provider packages. `elevenlabs` can mean speech-to-text or text-to-speech, but not chat. A provider key is not a promise that every family exists.

Provider selection does not replace the HPD runtime. The same `Agent` runtime still owns `RunAsync(...)`, tools, middleware, sessions, threads, events, permissions, compaction, hosting, and stored definitions.

## Selection, Construction, And Request Options

Provider selection identifies the provider, model, endpoint, and optional named authentication registration. Builder configuration supplies defaults; `AgentRunConfig` can replace them for one invocation.

Construction configuration is for values bound when a client is created, such as deployments, regions, API versions, SDK connection settings, and other provider-native constructor inputs. It is stored in the typed `ProviderConfig` payload on the selected family. Credentials are not provider configuration and are never serialized there.

Shared chat behavior and provider/model selection live together on
`ChatClientConfig`. Agent defaults use `AgentConfig.Clients.Chat`; a complete
per-invocation replacement uses `AgentRunConfig.Clients.Chat`.

Use `ChatClientConfig` for serializable chat controls:

- model selection
- temperature, top-p, top-k, seed, max output tokens, and stop sequences
- response format and structured-output shape
- generic reasoning options

Use the normal HPD tool registration APIs for model-callable tools. Advanced in-process integrations can still use MEAI `ChatOptions` where HPD exposes runtime-only hooks for non-serializable objects.

Use construction options for:

- provider-native service tiers, regions, deployments, hosted resource ids, or SDK connection settings
- provider-native content handling such as prompt-cache markers
- audio-specific voice, format, speed, transcript, or realtime settings
- advanced API controls that do not have a generic MEAI/HPD chat option

In C#, assign the provider package's typed configuration to `ProviderConfig`. In
JSON or YAML, put the provider-specific object under `providerConfig`. Request-only
provider settings belong under the typed `providerOptions` payload.

## Runtime Provider And Model Selection

One built agent can use different chat providers and models on consecutive or concurrent runs:

```csharp
await agent.RunAsync("Use OpenAI", runConfig: new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new ProviderReference { Key = "openai" },
            ModelName = "gpt-5-mini"
        }
    }
});

await agent.RunAsync("Use Anthropic", runConfig: new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new ProviderReference { Key = "anthropic" },
            ModelName = "claude-sonnet-4-5"
        }
    }
});
```

`AgentRunConfig.Clients.Chat` is the complete run-level chat replacement. Its
provider key, model, portable chat controls, authentication selection, typed
provider configuration, and typed provider options travel as one coherent unit.
An explicitly supplied runtime client remains a runtime-only override. HPD fails
when a provider key contradicts the provider-owned payload types; it never silently
ignores the mismatch.

## Secret Resolution

`BuildAsync()` creates and retains the default secret resolver chain:

1. explicit values registered by fluent provider helpers or `AddExplicitSecret(...)`
2. a resolver supplied with `WithSecretResolver(...)`, or the normal environment and additional resolver sources
3. configuration, when an `IConfiguration` is available

Explicit values passed to fluent setup remain runtime-only. Portable authentication
publishes only a secret key, account reference, or external-identity name; credential
material is never serialized. Do not put credentials in `ProviderConfig` or custom
headers.

Environment variables are the usual deployment path. Configuration lookup also supports provider-scoped keys such as:

```text
openai:ApiKey
Providers:openai:ApiKey
```

Configuration capitalization is mechanical. Prefer the exact provider key in docs and examples unless a provider page lists a specific alias.

Secret resolution happens when a run first needs a provider client, not while the agent is built. This means an application can build an agent without a provider or credential, and a configured default that is never selected never contacts its provider.

## Selecting A Static Credential

Use the portable authentication union on `ProviderReference` when a host has independently selectable credentials such as personal, work, or tenant-specific API keys. The selection contains a secret-resolver key, never the secret value:

```csharp
using HPD.Agent;
using HPD.Agent.Providers;

await agent.RunAsync("Use the work credential", runConfig: new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new ProviderReference
            {
                Key = "openai",
                Authentication = new ApiKeyProviderAuthentication
                {
                    SecretKey = "openai:work:ApiKey"
                }
            },
            ModelName = "gpt-5-mini"
        }
    }
});
```

The host must register the corresponding secret with its `ISecretResolver`. The serialized configuration contains the secret key, not the resolved value. `OAuthProviderAuthentication` selects a durable authorization account, and `ExternalIdentityProviderAuthentication` selects an SDK-native identity registered by the host.

## Setup Caveats

Provider creation can resolve secrets from environment variables or configuration. These are the practical setup boundaries:

| Setup path | Behavior |
| --- | --- |
| OpenAI, Azure OpenAI, Anthropic, Google AI, Hugging Face, Mistral | Env-backed fluent setup works when the documented environment variable is present. OpenAI and Anthropic also support env-backed `Clients.Chat` JSON setup. Missing credentials fail when a run first acquires that provider client, not during `BuildAsync()`. |
| Bedrock | Region env such as `AWS_REGION` / `AWS_DEFAULT_REGION` is accepted; credentials normally flow through the AWS SDK default credential chain. |
| Ollama | `OLLAMA_ENDPOINT` / `OLLAMA_HOST` apply to fluent `.WithOllama(...)`; JSON/config setup should include `Endpoint` when not using localhost. |

Keep an explicit credential diagnostic in application code when you need a friendlier error before the first run.

## Related Guides

- [Provider Setup Overview](../guides/providers/overview.md)
- [Provider Families](../reference/provider-families.md)
- [Provider Keys And Environment Variables](../reference/provider-keys-and-env-vars.md)
- [Audio Overview](../guides/audio/overview.md)
