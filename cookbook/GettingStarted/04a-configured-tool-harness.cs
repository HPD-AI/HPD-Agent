#:package HPD-Agent.Framework@0.9.0
#:package HPD-Agent.Providers.OpenAI@0.9.0
#:property TargetFramework=net10.0

// This sample shows typed harness settings, constructor DI, and a run override.

using HPD.Agent;
using HPD.Agent.Middleware;
using HPD.Agent.Providers.OpenAI;

var settings = new SearchSettings
{
    MaxResults = 3,
    Collection = "product-docs"
};

var runConfig = new AgentRunConfig();
runConfig.Harnesses
    .For<SearchHarness>()
    .Override(new SearchSettingsOverrides
    {
        MaxResults = 1
    });

var agent = await new AgentBuilder()
    .WithInstructions(
        "Use the search tool when the user asks about HPD Agent architecture.")
    .WithOpenAI("gpt-5-mini")
    .WithToolHarness<SearchHarness>(options => options
        .Configure(settings)
        .UseDependency<ISearchClient>(new InMemorySearchClient()))
    .BuildAsync();

var result = await agent.RunAsync(
    "Search the product docs for how tool harness configuration works.",
    runConfig: runConfig);

Console.WriteLine(result.Text);

public sealed record SearchSettings : IHarnessSettingsValidator
{
    [HarnessRunSetting]
    public int MaxResults { get; init; } = 5;

    public string Collection { get; init; } = "default";

    public void ValidateSettings()
    {
        if (MaxResults < 1)
            throw new AgentRunConfigurationException(
                "SearchSettingsInvalid",
                "config.maxResults",
                "MaxResults must be positive.");
    }
}

public sealed class SearchHarness(
    IHarnessConfig<SearchSettings> configuration,
    ISearchClient client)
{
    [AIFunction(Name = "search_product_docs")]
    [AIDescription("Searches the configured product documentation collection.")]
    public async Task<string> SearchAsync(
        [AIDescription("The search query.")] string query,
        FunctionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Capture one immutable execution setting before any detached work.
        var effective = configuration.For(context);
        var results = await client.SearchAsync(
            effective.Collection,
            query,
            effective.MaxResults,
            cancellationToken);

        return string.Join(
            "\n",
            results.Select(result => $"{result.Title}: {result.Summary}"));
    }
}

public interface ISearchClient
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collection,
        string query,
        int maxResults,
        CancellationToken cancellationToken);
}

public sealed record SearchResult(string Title, string Summary);

public sealed class InMemorySearchClient : ISearchClient
{
    private static readonly SearchResult[] Results =
    [
        new(
            "Harness configuration",
            "Typed immutable settings flow through IHarnessConfig<T>."),
        new(
            "Harness dependency injection",
            "One public constructor receives generated dependency activation."),
        new(
            "Harness run overrides",
            "Marked settings accept isolated per-run patches.")
    ];

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collection,
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var matches = Results
            .Where(result =>
                result.Title.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
                result.Summary.Contains("settings", StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SearchResult>>(matches);
    }
}
