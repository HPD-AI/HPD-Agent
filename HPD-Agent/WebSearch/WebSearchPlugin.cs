using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

/// <summary>
/// Web search plugin that provides intelligent search capabilities with provider-aware functions.
/// This plugin automatically adapts its available functions and descriptions based on configured providers.
/// </summary>
public class WebSearchPlugin
{
    private readonly WebSearchContext _context;

    /// <summary>
    /// Initializes a new instance of the WebSearchPlugin.
    /// </summary>
    /// <param name="context">WebSearch context containing configured providers.</param>
    public WebSearchPlugin(WebSearchContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
    
    // === Multi-Provider Functions (include provider parameter) ===
    
    [AIFunction]
    [Description("Search the web using {context.DefaultProvider}")]
    public async Task<string> WebSearch(
        [Description("Search query")] string query,
        [Description("Number of results to return")] int count = 5,
        [Description("Provider: {context.ConfiguredProviders} (optional, defaults to {context.DefaultProvider})")] 
        string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Search query cannot be empty", nameof(query));
        var connector = provider != null ? _context.GetConnector(provider) : _context.GetDefaultConnector();
        var result = await connector.SearchAsync(query, count);
        return !result.IsSuccess ? $"Search failed: {result.ErrorMessage}" : FormatSearchResults(result);
    }
    
    [AIFunction]
    [ConditionalFunction<WebSearchContext>("HasTavilyProvider || HasBraveProvider || HasBingProvider")]
    [Description("Search for recent news from the last {timeRange}")]
    public async Task<string> NewsSearch(
        [Description("News search query")] string query,
        [Description("Time range: day, week, month")] string timeRange = "week",
        [Description("Provider: {context.ConfiguredProviders} (optional, defaults to {context.DefaultProvider})")] 
        string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("News query cannot be empty", nameof(query));
        var connector = provider != null ? _context.GetConnector(provider) : _context.GetDefaultConnector();
        var result = await connector.SearchNewsAsync(query, timeRange);
        return !result.IsSuccess ? $"News search failed: {result.ErrorMessage}" : FormatSearchResults(result);
    }
    
    
    [AIFunction]
    [ConditionalFunction<WebSearchContext>("HasBraveProvider || HasBingProvider")]
    [Description("Search for videos using video-capable providers")]
    public async Task<string> VideoSearch(
        [Description("Video search query")] string query,
        [Description("Number of videos to return")] int count = 5,
        [Description("Provider: {context.ConfiguredProviders} (optional, defaults to {context.DefaultProvider})")] 
        string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Video query cannot be empty", nameof(query));
        var targetProvider = provider ?? _context.DefaultProvider; // Use direct property
        var connector = _context.GetConnector(targetProvider);
        var result = await connector.SearchVideosAsync(query, count);
        return !result.IsSuccess ? $"Video search failed: {result.ErrorMessage}" : FormatSearchResults(result);
    }
    
    // === Single-Provider Functions (no provider parameter) ===
    
    
    [AIFunction]
    [ConditionalFunction<WebSearchContext>("HasTavilyProvider")]
    [Description("Get AI-generated answers with cited sources using Tavily's advanced AI")]
    public async Task<string> AnswerSearch(
        [Description("Question to answer")] string query,
        [Description("Use advanced AI answers for more detailed responses")] bool useAdvanced = true)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Question cannot be empty", nameof(query));
        var connector = _context.GetConnector("tavily");
        var result = await connector.SearchWithAnswerAsync(query);
        return !result.IsSuccess ? $"Answer search failed: {result.ErrorMessage}" : FormatAnswerResult(result);
    }
    
    
    [AIFunction]
    [ConditionalFunction<WebSearchContext>("HasBingProvider")]
    [Description("Search for shopping deals and product prices using Bing Shopping")]
    public async Task<string> ShoppingSearch(
        [Description("Product search query")] string query,
        [Description("Number of results to return")] int count = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Product query cannot be empty", nameof(query));
        var connector = _context.GetConnector("bing");
        var result = await connector.SearchShoppingAsync(query, count);
        return !result.IsSuccess ? $"Shopping search failed: {result.ErrorMessage}" : FormatSearchResults(result);
    }
    
    
    [AIFunction]
    [ConditionalFunction<WebSearchContext>("HasBraveProvider || HasBingProvider")]
    [Description("Enhanced multi-provider search with advanced capabilities")]
    public async Task<string> EnhancedSearch(
        [Description("Search query")] string query,
        [Description("Number of results to return")] int count = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Search query cannot be empty", nameof(query));
        
        var availableConnectors = new List<IWebSearchConnector>();
        if (_context.HasProvider("brave")) availableConnectors.Add(_context.GetConnector("brave"));
        if (_context.HasProvider("bing")) availableConnectors.Add(_context.GetConnector("bing"));
        
        if (!availableConnectors.Any()) return "Enhanced search requires Brave or Bing provider to be configured.";
        
        if (availableConnectors.Count > 1)
        {
            var tasks = availableConnectors.Select(c => c.SearchAsync(query, count / availableConnectors.Count)).ToArray();
            var results = await Task.WhenAll(tasks);
            var combinedResults = new List<SearchItem>();
            var providerNames = new List<string>();
            
            foreach (var result in results.Where(r => r.IsSuccess))
            {
                combinedResults.AddRange(result.Items);
                providerNames.Add(result.ProviderName);
            }
            
            return FormatSearchResults(new SearchResult
            {
                Query = query, Items = combinedResults.Take(count).ToList(), ProviderName = string.Join(" + ", providerNames),
                ResponseTime = TimeSpan.FromMilliseconds(results.Max(r => r.ResponseTime.TotalMilliseconds))
            });
        }
        else
        {
            var result = await availableConnectors[0].SearchAsync(query, count);
            return result.IsSuccess ? FormatSearchResults(result) : $"Search failed: {result.ErrorMessage}";
        }
    }
    
    // === Helper Methods (Unchanged) ===
    private static string FormatSearchResults(SearchResult result)
    {
        if (!result.Items.Any()) return $"No results found for query: {result.Query}";
        var formatted = new System.Text.StringBuilder();
        formatted.AppendLine($"Search Results for '{result.Query}' (via {result.ProviderName}):");
        formatted.AppendLine($"Found {result.Items.Count} results in {result.ResponseTime.TotalMilliseconds:F0}ms\n");
        for (int i = 0; i < result.Items.Count; i++)
        {
            var item = result.Items[i];
            formatted.AppendLine($"{i + 1}. {item.Title}");
            formatted.AppendLine($"   URL: {item.Url}");
            if (!string.IsNullOrEmpty(item.Snippet)) formatted.AppendLine($"   Summary: {item.Snippet}");
            if (item.PublishedDate.HasValue) formatted.AppendLine($"   Published: {item.PublishedDate.Value:yyyy-MM-dd}");
            formatted.AppendLine();
        }
        return formatted.ToString();
    }
    
    private static string FormatAnswerResult(AnswerResult result)
    {
        if (!result.Items.Any() && string.IsNullOrEmpty(result.Answer)) return $"No answer found for question: {result.Query}";
        var formatted = new System.Text.StringBuilder();
        formatted.AppendLine($"AI Answer for '{result.Query}':\n");
        if (!string.IsNullOrEmpty(result.Answer)) formatted.AppendLine("Answer:\n" + result.Answer + "\n");
        if (result.Sources.Any())
        {
            formatted.AppendLine("Sources:");
            foreach (var source in result.Sources) formatted.AppendLine($"- {source}");
            formatted.AppendLine();
        }
        if (result.FollowUpQuestions.Any())
        {
            formatted.AppendLine("Related Questions:");
            foreach (var question in result.FollowUpQuestions) formatted.AppendLine($"- {question}");
            formatted.AppendLine();
        }
        if (result.Items.Any())
        {
            formatted.AppendLine($"Supporting Results ({result.Items.Count}):");
            for (int i = 0; i < Math.Min(result.Items.Count, 3); i++) formatted.AppendLine($"{i + 1}. {result.Items[i].Title} - {result.Items[i].Url}");
        }
        return formatted.ToString();
    }
}