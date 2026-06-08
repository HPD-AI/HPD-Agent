# Web Search Harness

The WebSearch harness exposes web search as model-callable tools. The public setup path is Tavily.

Use this harness when an agent needs to search the public web during a run and return source URLs, snippets, or Tavily answer results.

## Package And Namespace

Reference the WebSearch harness package or project, then import:

```csharp
using HPD.Agent;
using HPD.Agent.ToolHarness.WebSearch;
```

## Add Tavily Search

Configure Tavily on `AgentBuilder` before building the agent:

```csharp
var agent = await new AgentBuilder()
    .WithInstructions("Use web search when current facts or source links matter.")
    .WithOpenAI(openai => openai
        .WithApiKey(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
        .WithModel("gpt-4.1-mini"))
    .WithTavilyWebSearch(tavily => tavily
        .WithApiKey(Environment.GetEnvironmentVariable("TAVILY_API_KEY")!)
        .WithMaxResults(5))
    .BuildAsync();
```

`WithTavilyWebSearch(...)` creates a Tavily connector, creates a `WebSearchTools` harness with the matching `WebSearchContext`, and registers that configured harness with the agent.

## Configuration Key

The parameterless setup path resolves the key from configuration:

```csharp
var agent = await new AgentBuilder()
    .WithTavilyWebSearch()
    .BuildAsync();
```

That path reads:

```text
Tavily:ApiKey
```

If no key is available, setup fails with an `InvalidOperationException` whose message includes:

```text
Tavily API key is required
```

## Tavily Options

The Tavily builder supports common search controls:

```csharp
.WithTavilyWebSearch(tavily => tavily
    .WithApiKey(tavilyApiKey)
    .WithSearchDepth(TavilySearchDepth.Basic)
    .WithMaxResults(5)
    .WithTimeRange("week")
    .WithTopic("general")
    .IncludeAnswers()
    .IncludeRawContent(false)
    .IncludeImages(false))
```

`WithMaxResults(...)` accepts values from `1` through `20`. `WithTimeRange(...)` accepts `day`, `week`, `month`, or `year`. `WithTopic(...)` accepts Tavily topic values such as `general` and `news`.

## Tools Exposed

With Tavily configured, the harness can expose:

- `WebSearch`: general web search.
- `NewsSearch`: recent news search.
- `AnswerSearch`: Tavily answer search with cited sources.

The harness also contains functions for video, shopping, and enhanced multi-provider search, but those require providers that are not part of the Tavily setup path.

## Boundaries

Brave and Bing builder types exist in the WebSearch harness source, but their connector implementations are not a public setup path here. Document and ship Tavily first.

Tavily registration and missing-key behavior can be validated without making a live search call. A real search run still requires a valid Tavily API key and network access at runtime.

## Related Reading

- [Built-In Harnesses](overview.md)
- [Permissions](../middleware/permissions.md)
- [Sandboxing Overview](../sandboxing/overview.md)
- [Tools, Functions, And Harnesses](../../concepts/tools-functions-and-harnesses.md)
