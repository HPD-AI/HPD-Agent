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

Provider construction options stay focused on the DashScope connection and endpoint selection. Put model behavior on chat defaults or per-run chat config:

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
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.MaxOutputTokens = 4096;
        chat.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.High
        };
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

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "dashscope",
      "ModelName": "qwen-plus",
      "ChatDefaults": {
        "Temperature": 0.2,
        "MaxOutputTokens": 4096,
        "Reasoning": {
          "Effort": "High"
        },
        "AdditionalProperties": {
          "enable_search": true,
          "thinking_budget": 1024,
          "search_options": {
            "enableCitation": true,
            "searchStrategy": "turbo"
          }
        }
      },
      "ProviderOptions": {
        "workspaceId": "your-workspace-id",
        "timeoutSeconds": 120
      }
    }
  }
}
```

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
  "Clients": {
    "Embeddings": {
      "ProviderKey": "dashscope",
      "ModelName": "text-embedding-v4"
    }
  }
}
```

## Runtime Chat Options

Use `ChatRunConfig` for shared model-call behavior such as temperature, top-p, top-k, max output tokens, stop sequences, tools, tool mode, seed, and generic reasoning options. Put agent-level defaults in `ClientProviderConfig.ChatDefaults` or `.WithChatDefaults(...)`; use `AgentRunConfig.Chat` for per-run or per-session overrides.

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

## DashScope Provider Options

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

Set `Endpoint` on `ClientProviderConfig` or `baseAddress` in provider options for a custom HTTP base URL. `websocketBaseAddress`, `workspaceId`, `socketPoolSize`, and `timeoutSeconds` are available when the underlying DashScope SDK needs them.

For multimodal models, the adapter auto-detects common `qwen-vl`, `qwen3-vl`, `qwen3-omni`, and `gui-plus` model ids. Set `DefaultUseVl = true` when you need to force the multimodal endpoint by default; set `DashScopeChatRequestOptions.UseVl` for a single request.

| Option | Purpose |
| --- | --- |
| `BaseAddress` | DashScope HTTP API base address. `Endpoint` on `ClientProviderConfig` can also set a custom HTTP base URL. |
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

Validation checks API key, model, endpoint/base URL, websocket URL, workspace-related numeric options, multimodal options, and embedding options. Runtime chat options are applied through `ChatDefaults` and `AgentRunConfig.Chat`. Missing credentials fail at `BuildAsync()` before a live provider call.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
