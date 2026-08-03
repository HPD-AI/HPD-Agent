# Runtime Provider Selection And Authentication

An HPD agent is reusable behavior plus optional defaults. It is not permanently
bound to the provider configured by its builder. Every Chat run can select its own
provider, model, endpoint, construction settings, and static authentication.

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

Normal model-call behavior belongs to `ChatRunConfig` and does not create a new
client identity:

```csharp
run.Chat = new ChatRunConfig
{
    Temperature = 0.2,
    MaxOutputTokens = 2048
};
```

Provider and model selection belongs to `ProviderKey` and `ModelId`, not
`ChatRunConfig`.

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
