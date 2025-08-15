using System;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// Extension methods for AgentBuilder to configure web search providers with type-safe fluent builders
/// </summary>
public static class AgentBuilderWebSearchExtensions
{
    // Store connectors temporarily until we can integrate with AgentBuilder properly
    private static readonly Dictionary<AgentBuilder, List<IWebSearchConnector>> _pendingConnectors = new();
    
    /// <summary>
    /// Configures Tavily web search provider with fluent builder pattern
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration action for Tavily settings</param>
    /// <returns>The agent builder for chaining</returns>
    public static AgentBuilder WithWebSearchProvider(this AgentBuilder builder,
        Func<ITavilyWebSearchBuilder, ITavilyWebSearchBuilder> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        
        var tavilyBuilder = new TavilyWebSearchBuilder();
        var configuredBuilder = configure(tavilyBuilder);
        var connector = ((IWebSearchProviderBuilder<ITavilyWebSearchBuilder>)configuredBuilder).Build();
        
        return AddWebSearchConnector(builder, connector);
    }
    
    /// <summary>
    /// Configures Brave web search provider with fluent builder pattern
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration action for Brave settings</param>
    /// <returns>The agent builder for chaining</returns>
    public static AgentBuilder WithWebSearchProvider(this AgentBuilder builder,
        Func<IBraveWebSearchBuilder, IBraveWebSearchBuilder> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        
        var braveBuilder = new BraveWebSearchBuilder();
        var configuredBuilder = configure(braveBuilder);
        var connector = ((IWebSearchProviderBuilder<IBraveWebSearchBuilder>)configuredBuilder).Build();
        
        return AddWebSearchConnector(builder, connector);
    }
    
    /// <summary>
    /// Configures Bing web search provider with fluent builder pattern
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration action for Bing settings</param>
    /// <returns>The agent builder for chaining</returns>
    public static AgentBuilder WithWebSearchProvider(this AgentBuilder builder,
        Func<IBingWebSearchBuilder, IBingWebSearchBuilder> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        
        var bingBuilder = new BingWebSearchBuilder();
        var configuredBuilder = configure(bingBuilder);
        var connector = ((IWebSearchProviderBuilder<IBingWebSearchBuilder>)configuredBuilder).Build();
        
        return AddWebSearchConnector(builder, connector);
    }
    
    /// <summary>
    /// Adds a web search connector to the agent's capabilities and automatically configures the WebSearchPlugin
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="connector">The configured web search connector</param>
    /// <returns>The agent builder for chaining</returns>
    private static AgentBuilder AddWebSearchConnector(AgentBuilder builder, IWebSearchConnector connector)
    {
        // Store the connector for later collection during WithWebSearchPlugin()
        if (!_pendingConnectors.ContainsKey(builder))
        {
            _pendingConnectors[builder] = new List<IWebSearchConnector>();
        }
        
        _pendingConnectors[builder].Add(connector);
        return builder;
    }
    
    /// <summary>
    /// Finalizes web search configuration by creating the WebSearchPlugin with all configured providers.
    /// This method should be called after all WithWebSearchProvider() calls.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="defaultProvider">Name of the default provider to use (optional)</param>
    /// <returns>The agent builder for chaining</returns>
    public static AgentBuilder WithWebSearchPlugin(this AgentBuilder builder, string? defaultProvider = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        
        // Collect all registered connectors for this builder
        if (!_pendingConnectors.TryGetValue(builder, out var connectors) || !connectors.Any())
        {
            throw new InvalidOperationException("No web search providers configured. Call WithWebSearchProvider() before WithWebSearchPlugin().");
        }
        
        // Create the WebSearchContext with all connectors
        var context = new WebSearchContext(connectors, defaultProvider);
        
        // Register the WebSearchPlugin with the context
        var plugin = new WebSearchPlugin(context);
        builder.WithPlugin(plugin, context);
        
        // Clean up the temporary storage
        _pendingConnectors.Remove(builder);
        
        return builder;
    }
}

/// <summary>
/// Placeholder implementations for other providers
/// These will be implemented in future iterations
/// </summary>
public class BraveWebSearchBuilder : IBraveWebSearchBuilder
{
    private readonly BraveConfig _config = new();
    
    public IBraveWebSearchBuilder WithApiKey(string apiKey)
    {
        _config.ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }
    
    public IBraveWebSearchBuilder WithTimeout(TimeSpan timeout)
    {
        _config.Timeout = timeout;
        return this;
    }
    
    public IBraveWebSearchBuilder WithRetryPolicy(int retries, TimeSpan delay)
    {
        _config.RetryCount = retries;
        _config.RetryDelay = delay;
        return this;
    }
    
    public IBraveWebSearchBuilder OnError(Action<Exception> errorHandler)
    {
        _config.ErrorHandler = errorHandler;
        return this;
    }
    
    public IBraveWebSearchBuilder WithSafeSearch(BraveSafeSearch safeSearch)
    {
        _config.SafeSearch = safeSearch;
        return this;
    }
    
    public IBraveWebSearchBuilder WithCountry(string countryCode)
    {
        _config.Country = countryCode;
        return this;
    }
    
    public IBraveWebSearchBuilder WithSearchLanguage(string language)
    {
        _config.SearchLanguage = language;
        return this;
    }
    
    public IBraveWebSearchBuilder WithUILanguage(string uiLanguage)
    {
        _config.UILanguage = uiLanguage;
        return this;
    }
    
    public IBraveWebSearchBuilder WithResultFilter(string filter)
    {
        _config.ResultFilter = filter;
        return this;
    }
    
    public IBraveWebSearchBuilder WithUnits(BraveUnits units)
    {
        _config.Units = units;
        return this;
    }
    
    public IBraveWebSearchBuilder EnableSpellCheck(bool enable = true)
    {
        _config.SpellCheck = enable;
        return this;
    }
    
    public IBraveWebSearchBuilder EnableExtraSnippets(bool enable = true)
    {
        _config.ExtraSnippets = enable;
        return this;
    }
    
    public IBraveWebSearchBuilder ForPrivacyFocusedSearch()
    {
        return this
            .WithSafeSearch(BraveSafeSearch.Strict)
            .WithResultFilter("web")
            .EnableSpellCheck(false)
            .WithCountry("ALL");
    }
    
    public IBraveWebSearchBuilder ForDeveloperSearch()
    {
        return this
            .WithResultFilter("web,news")
            .WithSearchLanguage("en")
            .WithCountry("US")
            .EnableExtraSnippets(true);
    }
    
    IWebSearchConnector IWebSearchProviderBuilder<IBraveWebSearchBuilder>.Build()
    {
        _config.Validate();
        // TODO: Implement BraveConnector
        throw new NotImplementedException("BraveConnector implementation coming in next iteration");
    }
}

public class BingWebSearchBuilder : IBingWebSearchBuilder
{
    private readonly BingConfig _config = new();
    
    public IBingWebSearchBuilder WithApiKey(string apiKey)
    {
        _config.ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }
    
    public IBingWebSearchBuilder WithTimeout(TimeSpan timeout)
    {
        _config.Timeout = timeout;
        return this;
    }
    
    public IBingWebSearchBuilder WithRetryPolicy(int retries, TimeSpan delay)
    {
        _config.RetryCount = retries;
        _config.RetryDelay = delay;
        return this;
    }
    
    public IBingWebSearchBuilder OnError(Action<Exception> errorHandler)
    {
        _config.ErrorHandler = errorHandler;
        return this;
    }
    
    public IBingWebSearchBuilder WithEndpoint(string endpoint)
    {
        _config.Endpoint = endpoint;
        return this;
    }
    
    public IBingWebSearchBuilder WithMarket(string market)
    {
        _config.Market = market;
        return this;
    }
    
    public IBingWebSearchBuilder WithSafeSearch(BingSafeSearch safeSearch)
    {
        _config.SafeSearch = safeSearch;
        return this;
    }
    
    public IBingWebSearchBuilder WithFreshness(BingFreshness freshness)
    {
        _config.Freshness = freshness;
        return this;
    }
    
    public IBingWebSearchBuilder WithResponseFilter(string filter)
    {
        _config.ResponseFilter = filter;
        return this;
    }
    
    public IBingWebSearchBuilder EnableShoppingSearch(bool enable = true)
    {
        _config.ShoppingSearch = enable;
        return this;
    }
    
    public IBingWebSearchBuilder WithTextDecorations(bool enable = true)
    {
        _config.TextDecorations = enable;
        return this;
    }
    
    public IBingWebSearchBuilder WithTextFormat(BingTextFormat format)
    {
        _config.TextFormat = format;
        return this;
    }
    
    public IBingWebSearchBuilder ForEnterpriseSearch()
    {
        return this
            .WithResponseFilter("webpages,images,news")
            .EnableShoppingSearch(true)
            .WithTextDecorations(true);
    }
    
    IWebSearchConnector IWebSearchProviderBuilder<IBingWebSearchBuilder>.Build()
    {
        _config.Validate();
        // TODO: Implement BingConnector
        throw new NotImplementedException("BingConnector implementation coming in next iteration");
    }
}

// Placeholder config classes - will be implemented fully in next iteration

public class BraveConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan? Timeout { get; set; }
    public int? RetryCount { get; set; }
    public TimeSpan? RetryDelay { get; set; }
    public Action<Exception>? ErrorHandler { get; set; }
    public BraveSafeSearch SafeSearch { get; set; }
    public string? Country { get; set; }
    public string? SearchLanguage { get; set; }
    public string? UILanguage { get; set; }
    public string? ResultFilter { get; set; }
    public BraveUnits Units { get; set; }
    public bool SpellCheck { get; set; } = true;
    public bool ExtraSnippets { get; set; } = false;
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Brave API key is required");
    }
}

public class BingConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan? Timeout { get; set; }
    public int? RetryCount { get; set; }
    public TimeSpan? RetryDelay { get; set; }
    public Action<Exception>? ErrorHandler { get; set; }
    public string? Endpoint { get; set; }
    public string? Market { get; set; }
    public BingSafeSearch SafeSearch { get; set; }
    public BingFreshness Freshness { get; set; }
    public string? ResponseFilter { get; set; }
    public bool ShoppingSearch { get; set; } = false;
    public bool TextDecorations { get; set; } = true;
    public BingTextFormat TextFormat { get; set; }
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Bing API key is required");
    }
}
