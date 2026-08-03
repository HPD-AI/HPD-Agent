# Runtime Provider Selection And Authentication

An HPD agent is reusable behavior plus optional defaults. It is not permanently
bound to the provider configured by its builder. Every Chat run can select its own
provider, model, endpoint, construction settings, and static authentication.

## Understand The Configuration Planes First

Provider configuration is intentionally split by when a value is consumed. This
prevents a request-only setting such as temperature from being mistaken for a reason
to construct another SDK client, and prevents OpenAI construction state from leaking
into an Anthropic run.

| Plane | Public surfaces | Typical values | When applied |
| --- | --- | --- | --- |
| Family selection | `AgentClientConfig`, `ClientProviderConfig`, and the Chat aliases on `AgentRunConfig` | provider, model, endpoint, authentication name, non-secret headers | while resolving the run |
| General construction | `ClientProviderConfig` | endpoint and provider attribution fields | while acquiring a client |
| Provider-specific construction | typed provider config stored through `SetProviderConfig(...)`, serialized as `ConstructionOptions` | OpenAI API choice/project, Azure API version, Bedrock region, Google platform/project | while constructing a client |
| General request | `ChatRunConfig` | temperature, top-p, top-k, token limit, penalties, seed, stop sequences, reasoning, response format | for each model request |
| Provider-specific request | typed helpers such as `UseAnthropicChatRequestOptions(...)`, carried in `ChatRunConfig.AdditionalProperties` | service tier, prompt caching, exact thinking controls | for each model request |
| Runtime behavior | the rest of `AgentRunConfig` | instructions, context, middleware, attachments, transport, compaction, security, structured output | for that agent invocation |

The important division is not "general versus provider-specific" by itself. Both
general and provider-specific settings can be either construction-time or
request-time settings.

### Shared Provider Defaults And Family Defaults

`AgentClientConfig.Providers` stores reusable defaults by provider key.
`AgentClientConfig.Chat`, `Realtime`, `Embeddings`, and the other family properties
select a provider for one client family and can override those shared defaults:

```json
{
  "Clients": {
    "Providers": {
      "openai": {
        "ProviderKey": "openai",
        "Endpoint": "https://api.openai.com/v1",
        "AuthenticationKey": "openai-work",
        "CustomHeaders": {
          "X-Tenant": "tenant-42"
        },
        "ConstructionOptions": {
          "projectId": "project-default",
          "networkTimeoutMs": 60000
        }
      }
    },
    "Chat": {
      "ProviderKey": "openai",
      "ModelName": "gpt-5-mini",
      "ChatDefaults": {
        "temperature": 0.2,
        "maxOutputTokens": 4096
      },
      "ConstructionOptions": {
        "projectId": "project-chat"
      }
    }
  }
}
```

For this Chat family, `project-chat` overrides `project-default`, while
`networkTimeoutMs` is inherited. The endpoint, authentication name, and safe custom
header are inherited from the shared `openai` entry.

The same shape is available at run time through `AgentRunConfig.Clients`. This is
useful when a run needs full family-shaped configuration instead of the top-level
Chat convenience properties.

### General Request Configuration

`ChatRunConfig` is the provider-neutral request layer. Its serializable fields are:

- `Temperature`, `TopP`, `TopK`, and `MaxOutputTokens`
- `FrequencyPenalty`, `PresencePenalty`, and `Seed`
- `StopSequences`
- `Reasoning`
- `AdditionalProperties`

`ResponseFormat` is also available for in-process calls but is runtime-only because
the MEAI response-format object is not generally serializable. Prefer HPD structured
output APIs when the response schema must cross a serialization boundary.

`ChatRunConfig.ModelId` still exists as a low-level MEAI request override, but
provider selection should use `AgentRunConfig.ModelId` or
`AgentRunConfig.Clients.Chat.ModelName`. Those selection properties are the
authoritative and unambiguous path when runs can switch providers.

Agent defaults belong in `ClientProviderConfig.ChatDefaults` or
`.WithChatDefaults(...)`. Non-null fields in `AgentRunConfig.Chat` override those
defaults for that run. Additional-property dictionaries merge by key, with the run
value winning.

### Provider-Specific Construction Configuration

Provider packages expose typed construction classes. Prefer those classes in C# so
invalid names and value types are caught before serialization:

```csharp
var chat = new ClientProviderConfig
{
    ProviderKey = "openai",
    ModelName = "gpt-5-mini"
};

chat.SetProviderConfig(new OpenAIProviderConfig
{
    ChatApi = OpenAIChatApi.Responses,
    ProjectId = "project-42",
    NetworkTimeoutMs = 60000
});
```

`SetProviderConfig(...)` writes the provider's source-generated representation into
the outer `ConstructionOptions` object. JSON and YAML users write that object
directly. Construction options are shallow-merged by property name: provider-shared
values apply first, then family values, then run-shared values, then run-family
values. A higher-priority property replaces the whole value at that property; nested
objects are not recursively merged.

### Provider-Specific Request Configuration

Provider-native request controls do not belong in `ConstructionOptions`. Use the
provider's typed request helper, which safely writes the required keys into
`ChatRunConfig.AdditionalProperties`:

```csharp
var run = new AgentRunConfig
{
    ProviderKey = "anthropic",
    ModelId = "claude-sonnet-4-5",
    Chat = new ChatRunConfig
    {
        Temperature = 0.2,
        MaxOutputTokens = 4096
    }
};

run.Chat.UseAnthropicChatRequestOptions(new AnthropicChatRequestOptions
{
    ServiceTier = AnthropicServiceTier.Auto,
    ThinkingBudgetTokens = 4096,
    CacheControl = new AnthropicCacheControlConfig
    {
        SystemMessages = AnthropicCacheTtl.OneHour
    }
});
```

The portable and Anthropic-specific values travel on the same request, but remain
separate from the Anthropic client construction identity.

### Resolution And Merge Order

For a selected family, HPD resolves configuration from lowest to highest priority:

1. agent-level shared defaults for the selected provider
2. agent-level family configuration, only when it belongs to that same provider
3. run-level shared defaults for the selected provider
4. run-level family configuration

Scalar values use the highest non-null/non-empty value. Custom headers merge by
case-insensitive header name. Construction options shallow-merge by JSON property.
`ChatDefaults` is selected as a complete default object at this stage; individual
request fields are subsequently merged when `AgentRunConfig.Chat` is applied.

If the run changes the provider, step 2 is deliberately skipped. The old provider's
model, endpoint, authentication, headers, and construction options are stale for the
new provider. HPD instead starts from the new provider's shared defaults and applies
the new run selection.

## Configure An Optional Default

Builder helpers remain the shortest way to define the default run selection:

```csharp
var agent = await new AgentBuilder()
    .WithAnthropic("claude-sonnet-4-5")
    .BuildAsync();
```

`BuildAsync()` records the Anthropic selection. It does not resolve
`anthropic:ApiKey` or create an Anthropic chat client. The first run that uses this
default performs that work lazily.

An agent can also be built without any Chat default:

```csharp
var agent = await new AgentBuilder().BuildAsync();
```

Such an agent must receive a provider/model selection or an explicit chat client
when it runs.

## Switch Per Run

Top-level `AgentRunConfig` properties are Chat convenience aliases:

```csharp
await agent.RunAsync("Use OpenAI", new AgentRunConfig
{
    ProviderKey = "openai",
    ModelId = "gpt-5-mini"
});

await agent.RunAsync("Use Anthropic", new AgentRunConfig
{
    ProviderKey = "anthropic",
    ModelId = "claude-sonnet-4-5"
});
```

The same agent supports consecutive and concurrent runs with different selections.
For family-shaped configuration, set `AgentRunConfig.Clients.Chat` instead.

Selection precedence for Chat is:

1. `OverrideChatClient`
2. run family selection
3. top-level run convenience selection
4. agent default
5. an inherited parent client when the invocation supports inheritance

Changing providers starts a new provider state boundary. HPD does not carry the old
provider's model, endpoint, authentication name, headers, or construction options
into the new provider.

## Separate Construction From Requests

Values that determine how an SDK client is constructed belong to
`ConstructionOptions`:

```csharp
var run = new AgentRunConfig
{
    ProviderKey = "openai",
    ModelId = "gpt-5-mini"
};

run.ConstructionOptions = JsonDocument.Parse(
    """{"projectId":"project-42"}""").RootElement.Clone();
```

Normal model-call behavior belongs to `ChatRunConfig`:

```csharp
run.Chat = new ChatRunConfig
{
    Temperature = 0.2,
    MaxOutputTokens = 2048
};
```

Provider and model selection should use `ProviderKey` and `ModelId`, rather than the
low-level `ChatRunConfig.ModelId` request override. Most request options do not alter
client identity. The current cache also includes effective max-output-token settings
when they are client-bound by the resolved configuration.

## Canonical Static Credentials

With no per-run authentication selection, a provider resolves its canonical secret
through `ISecretResolver`, such as `openai:ApiKey` or `anthropic:ApiKey`. Environment
aliases and `IConfiguration` continue to participate in the normal resolver chain.

Fluent `apiKey` arguments remain runtime-only. Depending on the provider helper they
are held by explicit secret state or an ignored in-process configuration field; in
either case the value is excluded from serialized agent configuration.

For a one-off in-process run, `AgentRunConfig.ApiKey` has highest authentication
priority:

```csharp
await agent.RunAsync("One-off call", new AgentRunConfig
{
    ProviderKey = "openai",
    ModelId = "gpt-5-mini",
    ApiKey = temporaryKey
});
```

`ApiKey` is ignored by JSON serialization. A client created from a direct per-run
key is isolated from the reusable cache and disposed when its lease ends.

## Several Credentials For One Provider

Named registrations let a host expose several independently selectable API keys
without making profiles or secret persistence part of HPD Agent:

```csharp
using HPD.Agent.Providers;
using HPD.Agent.Secrets;
using Microsoft.Extensions.DependencyInjection;

var authentication = new InMemoryProviderAuthenticationRegistry();
authentication.Register(new ProviderAuthenticationRegistration
{
    Key = "openai-personal",
    ProviderKey = "openai",
    SecretKey = "openai:personal:ApiKey"
});
authentication.Register(new ProviderAuthenticationRegistration
{
    Key = "openai-work",
    ProviderKey = "openai",
    SecretKey = "openai:work:ApiKey",
    IsDefault = true
});

var services = new ServiceCollection()
    .AddSingleton<IProviderAuthenticationRegistry>(authentication)
    .AddSingleton<ISecretResolver>(new ExplicitSecretResolver(new Dictionary<string, string>
    {
        ["openai:personal:ApiKey"] = personalKey,
        ["openai:work:ApiKey"] = workKey
    }))
    .BuildServiceProvider();

var agent = await new AgentBuilder()
    .WithServiceProvider(services)
    .BuildAsync();
```

Select a registration by its opaque name:

```csharp
await agent.RunAsync("Use work", new AgentRunConfig
{
    ProviderKey = "openai",
    ModelId = "gpt-5-mini",
    AuthenticationKey = "openai-work"
});
```

The serialized run contains `AuthenticationKey`, never the API key. A receiving
worker must have a compatible registration and secret resolver. Multi-user hosts
should implement `IProviderAuthenticationRegistry` themselves and filter results
using `ProviderAuthenticationContext`.

HPD never chooses arbitrarily among several compatible registrations. With no
explicit selection, exactly one registration may be marked `IsDefault`; otherwise
the run fails with `AuthenticationSelectionRequired`.

## Client Reuse And Ownership

Equivalent provider construction identities share one lazily created Chat client.
Provider, model, authentication registration, endpoint, normalized headers,
construction options, and client-bound output settings contribute to that identity.
Request-only Chat options do not.

Concurrent first-use requests use single-flight construction. Cancelling one waiter
does not cancel shared construction, and a failed construction is evicted so a later
run can retry. Cached clients are disposed with the agent. Explicit
`OverrideChatClient` instances are borrowed and are not disposed by HPD.

## Serialization And Security

- `ApiKey` is never serialized.
- `AuthenticationKey` is safe metadata and is serialized.
- `ConstructionOptions` must not contain credentials.
- Authentication through `Authorization`, `Proxy-Authorization`, `api-key`, or
  `x-api-key` custom headers is rejected.
- Registration names identify credentials; they are not authorization. The host
  registry remains responsible for user and tenant access.

OAuth is outside this static-authentication model. Existing MCP OAuth remains a
separate subsystem.
