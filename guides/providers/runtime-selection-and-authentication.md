# Runtime Provider Selection And Authentication

An HPD agent is reusable behavior plus optional defaults. A run can replace the
complete configuration of any client family, so one agent can switch providers and
models between concurrent invocations.

## Configure An Optional Default

Builder helpers remain the shortest way to define an agent default:

```csharp
var agent = await new AgentBuilder()
    .WithAnthropic("claude-sonnet-4-5")
    .BuildAsync();
```

The builder records a `ChatClientConfig`; it does not eagerly resolve a credential
or create an SDK client. An agent may also be built without a chat default:

```csharp
var agent = await new AgentBuilder().BuildAsync();
```

That agent must receive a chat selection at run time or inherit one through an
explicit orchestration policy.

## Replace Chat Per Run

`AgentRunConfig.Clients.Chat` is the complete per-run chat replacement:

```csharp
using HPD.Agent;
using HPD.Agent.Providers;

await agent.RunAsync("Use OpenAI", runConfig: new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new ProviderReference { Key = "openai" },
            ModelName = "gpt-5-mini",
            Temperature = 0.2,
            MaxOutputTokens = 2048
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

There are no top-level provider/model/chat convenience aliases and no merge mode.
If a run supplies `Clients.Chat`, that object is authoritative for the invocation.
If it does not, the agent default applies.

Portable properties such as temperature and maximum output tokens share this object
with selection because they all describe the same effective chat client. A borrowed
in-process client can be supplied through `ChatClientConfig.Override`; it is
runtime-only and is not disposed by HPD.

## Typed Provider Configuration

Provider packages may add two typed payloads:

- `ProviderConfig` affects reusable SDK-client construction and caching.
- `ProviderOptions` affects chat requests made with that selected provider.

For example, Anthropic request options remain attached to the Anthropic selection:

```csharp
var chat = new ChatClientConfig
{
    Provider = new ProviderReference { Key = "anthropic" },
    ModelName = "claude-sonnet-4-5",
    ProviderOptions = new AnthropicChatRequestOptions
    {
        ThinkingBudgetTokens = 4096
    }
};
```

The provider key and typed payload must agree. HPD fails with a configuration error
when they contradict each other; it never silently ignores provider-specific data.
When switching providers, construct a new coherent family config instead of carrying
the previous provider's payload forward.

## JSON And YAML

JSON and YAML use the same family shape as C#. Portable fields and typed provider
payloads remain siblings:

```json
{
  "clients": {
    "chat": {
      "provider": {
        "key": "anthropic"
      },
      "modelName": "claude-sonnet-4-5",
      "temperature": 0.2,
      "providerConfig": {},
      "providerOptions": {
        "thinkingBudgetTokens": 4096
      }
    }
  }
}
```

```yaml
clients:
  chat:
    provider:
      key: anthropic
    modelName: claude-sonnet-4-5
    temperature: 0.2
    providerConfig: {}
    providerOptions:
      thinkingBudgetTokens: 4096
```

Provider profiles can supply reusable provider-owned payloads. The outer profile key
is explicit because `providerProfiles` is an array of provider/backend baselines:

```yaml
providerProfiles:
  - providerKey: anthropic
    backendKey: platform
    clients:
      chat:
        providerOptions:
          thinkingBudgetTokens: 4096
```

Use `HpdAgentConfigSerializer` with the generated composition. Plain source-generated
serialization cannot bind interface-typed provider payloads by itself.

## Static Credentials

With no explicit authentication selection, the provider resolves its canonical
secret through `ISecretResolver`, such as `openai:ApiKey` or
`anthropic:ApiKey`. Environment aliases, application configuration, and registered
secret sources continue to participate in that resolver chain.

The fluent provider overload accepts a literal in-process key. It copies the
characters into process-local, clearable storage and never serializes them:

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini", apiKey: temporaryKey)
    .BuildAsync();
```

For a portable key reference, select the secret-resolver key rather than the
secret value:

```csharp
await agent.RunAsync("Use work", runConfig: new AgentRunConfig
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

Only the secret key is serialized. The receiving host must install an
`ISecretResolver` that can resolve it.

For a runtime-only explicit key in a later run, register it on the builder before
building and attach the returned authentication selection to the run:

```csharp
var builder = new AgentBuilder();
var authentication = builder.RegisterExplicitApiKey(temporaryKey);
var agent = await builder.BuildAsync();

await agent.RunAsync("Use the explicit runtime credential", runConfig: new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            Provider = new ProviderReference
            {
                Key = "openai",
                Authentication = authentication
            },
            ModelName = "gpt-5-mini"
        }
    }
});
```

Portable authentication also supports `OAuthProviderAuthentication` for a durable
provider account and `ExternalIdentityProviderAuthentication` for an SDK-native
identity registered by the host. Authentication values are never placed in
`ProviderConfig` or custom headers.

## Provider Composition And Serialization

Applications reference the provider packages they use and install the generated,
immutable composition:

```csharp
builder.Services.AddHpdGeneratedProviders();
```

The composition contains descriptors, factories, typed serialization contracts, and
secret aliases without module initializers or reflection scanning. Use
`HpdAgentConfigSerializer` with that `ProviderComposition` for JSON or YAML carrying
typed provider payloads. Hosted input-event and TUI boundaries must receive the same
composition so per-run payloads are preserved.

## Client Reuse And Ownership

Equivalent construction identities share a lazily created client. Provider, model,
authentication selection, endpoint, normalized headers, typed provider config,
and client-bound settings contribute to that identity. Request-only provider options
do not.

Concurrent first use is single-flight. Failed construction is evicted for retry,
cached clients are disposed with their owner, and explicitly overridden clients are
borrowed.

Provider OAuth account selection is part of `ProviderReference.Authentication`.
MCP OAuth remains a separate subsystem for MCP servers.
