# DashScope

The DashScope provider uses provider key `dashscope` and the `HPD-Agent.Providers.DashScope` package. `ModelName` is a DashScope or Qwen chat model id for chat, or a DashScope embedding model id for embeddings.

The provider package is currently `net10.0` only. It uses the Cnblogs DashScope Microsoft.Extensions.AI adapter, which targets `net8.0` and depends on `Microsoft.Extensions.AI.Abstractions` 10.7.0.

Set an API key with the primary environment variable:

```bash
export DASHSCOPE_API_KEY="..."
```

The provider also registers `QWEN_API_KEY` and `DASHSCOPE_KEY` as API key aliases.

Use fluent chat setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.DashScope;

var agent = await new AgentBuilder()
    .WithDashScope(model: "qwen-plus")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about DashScope setup.");
Console.WriteLine(result.Text);
```

Provider configuration stay focused on the DashScope connection and endpoint selection. Put model behavior on the agent's `Clients.Chat` default or a run's complete `Clients.Chat` replacement:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.DashScope;

var agent = await new AgentBuilder()
    .WithDashScope(
        model: "qwen-plus",
        configure: dashScope =>
        {
            dashScope.WorkspaceId = "your-workspace-id";
            dashScope.TimeoutSeconds = 120;
        })

    .WithDashScopeChatRequestOptions(dashScope =>
    {
        dashScope.EnableSearch = true;
        dashScope.ThinkingBudget = 1024;
        dashScope.SearchOptions = new DashScopeSearchRequestOptions
        {
            EnableCitation = true,
            SearchStrategy = DashScopeSearchStrategy.Turbo
        };
    })
    .BuildAsync();
```

The provider helper writes the same portable fields, typed `providerConfig`, and typed `providerOptions` into `Clients.Chat`. Use the [runtime selection guide](runtime-selection-and-authentication.md) for the canonical JSON and YAML shape.

## Embeddings

DashScope also registers the embeddings provider family under the same `dashscope` key:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.DashScope;

var agent = await new AgentBuilder()
    .WithDashScopeEmbeddings(model: "text-embedding-v4")
    .BuildAsync();
```

The equivalent family config is:

```json
{
  "clients": {
    "embeddings": {
      "provider": {
        "key": "dashscope"
      },
      "modelName": "text-embedding-v4"
    }
  }
}
```

## Runtime Chat Options

Use `ChatClientConfig` for shared model-call behavior such as temperature, top-p, top-k, max output tokens, stop sequences, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `AgentConfig.Clients.Chat`; use `AgentRunConfig.Clients.Chat` for complete per-run replacements.

Use `DashScopeChatRequestOptions` for DashScope-only request properties:

| Option | DashScope key | Meaning |
| --- | --- | --- |
| `UseVl` | `useVl` | Forces the request to use DashScope multimodal generation endpoints. |
| `EnableSearch` | `enable_search` | Enables DashScope web search for the request. |
| `EnableTextImageMixed` | `enable_text_image_mixed` | Allows image tags to appear in search-augmented text output. |
| `SearchOptions` | `search_options` | Web-search controls. `EnableSearch` should also be enabled. |
| `ThinkingBudget` | `thinking_budget` | Exact maximum thinking-content length for supported models. Use generic `Reasoning` for normal reasoning control. |
| `PreserveThinking` | `preserve_thinking` | Makes previous reasoning content in chat history visible to the model. |
| `EnableCodeInterpreter` | `enable_code_interpreter` | Enables DashScope's internal code interpreter. This cannot be combined with normal tools. |
| `Logprobs` | `logprobs` | Requests output token log probabilities. |
| `TopLogprobs` | `top_logprobs` | Number of likely token alternatives to return with log probabilities. |
| `N` | `n` | Number of choices the model should generate. |
| `LogitBias` | `logit_bias` | Token id bias map. Values near `-100` ban a token; values near `100` force selection. |
| `TranslationOptions` | `translation_options` | Translation controls for Qwen-MT models. |
| `CacheControl` | `cache_control` | Cache-control options for supported DashScope models such as qwen-coder. |
| `VlHighResolutionImages` | `vl_high_resolution_images` | Allows higher-resolution image inputs for multimodal models. |
| `NegativePrompt` | `negative_prompt` | Negative prompt for supported multimodal or image-generation requests. |

`DashScopeSearchRequestOptions` supports:

| Option | Meaning |
| --- | --- |
| `EnableSource` | Includes search source information in the response. |
| `EnableCitation` | Includes citations in generated output. |
| `CitationFormat` | Citation format. DashScope defaults to a numbered bracket format. |
| `ForcedSearch` | Forces the model to use web search. |
| `SearchStrategy` | Search strategy, such as `DashScopeSearchStrategy.Turbo` or `DashScopeSearchStrategy.Max`. |
| `EnableSearchExtension` | Enables enhanced search for supported areas. |
| `PrependSearchResult` | Returns search results first when incremental output is enabled. |
| `Freshness` | Limits search content to a recent window in days, such as `7`, `30`, `180`, or `365`. |
| `AssignedSiteList` | Restricts search to the specified sites. |

`DashScopeTranslationRequestOptions` supports `SourceLang`, `TargetLang`, and `Domains`. Use `SourceLang = "auto"` to enable automatic detection.

`DashScopeCacheControlRequestOptions` supports `Type`; DashScope defaults to `DashScopeCacheControlType.Ephemeral`.

## DashScope Construction Options

`DashScopeProviderConfig` exposes DashScope connection, multimodal, and embedding settings:

```csharp
var agent = await new AgentBuilder()
    .WithDashScope(
        model: "qwen-plus",
        configure: options =>
        {
            options.WorkspaceId = "your-workspace-id";
            options.DefaultUseVl = true;
        })
    .BuildAsync();
```

Set `Endpoint` on `ChatClientConfig` or `baseAddress` in provider configuration for a custom HTTP base URL. `websocketBaseAddress`, `workspaceId`, `socketPoolSize`, and `timeoutSeconds` are available when the underlying DashScope SDK needs them.

For multimodal models, the adapter auto-detects common `qwen-vl`, `qwen3-vl`, `qwen3-omni`, and `gui-plus` model ids. Set `DefaultUseVl = true` when you need to force the multimodal endpoint by default; set `DashScopeChatRequestOptions.UseVl` for a single request.

| Option | Purpose |
| --- | --- |
| `BaseAddress` | DashScope HTTP API base address. `Endpoint` on `ChatClientConfig` can also set a custom HTTP base URL. |
| `WebsocketBaseAddress` | DashScope websocket API base address. |
| `WorkspaceId` | Workspace id sent with DashScope requests that require workspace scoping. |
| `SocketPoolSize` | Internal websocket pool size used by the DashScope SDK. |
| `TimeoutSeconds` | Request timeout in seconds for the underlying DashScope HTTP client. |
| `DefaultUseVl` | Forces multimodal generation endpoints by default. Leave unset to let the adapter infer from common VL, omni, and GUI model ids. |
| `EmbeddingModelId` | Default embedding model id for the embeddings provider family. |
| `EmbeddingDimensions` | Optional embedding dimensions. |

## Caveats

DashScope chat and embeddings are available only when the `HPD-Agent.Providers.DashScope` package is referenced by a `net10.0` application.

The provider uses the Cnblogs adapter rather than a custom protocol implementation. Provider metadata reports streaming, function calling, reasoning, and vision because the adapter maps MEAI streaming, tools, reasoning content, and multimodal content to DashScope APIs.

Generic HPD reasoning maps to DashScope `enable_thinking` on the currently referenced `Cnblogs.DashScope.AI` package. More granular DashScope reasoning fields such as reasoning effort are not exposed by that package version.

Runtime validation checks model, endpoint/base URL, websocket URL, workspace-related numeric options, multimodal options, and embedding options. Runtime chat options are applied through `Clients.Chat` and `AgentRunConfig.Clients.Chat`. Credentials are resolved lazily when a run first acquires this provider client; a missing credential fails that run, not `BuildAsync()`.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
