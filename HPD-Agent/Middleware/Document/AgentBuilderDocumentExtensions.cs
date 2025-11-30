using HPD.Agent;
using HPD.Agent.Middleware.Document;
using HPD_Agent.TextExtraction;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Extension methods for adding document handling middleware to AgentBuilder.
/// </summary>
public static class AgentBuilderDocumentExtensions
{
    /// <summary>
    /// Adds document handling middleware with full-text extraction strategy.
    /// Automatically creates a TextExtractionUtility instance if not already present.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="options">Document handling options (optional)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// var agent = new AgentBuilder()
    ///     .WithChatClient(client)
    ///     .WithDocumentHandling() // Uses default TextExtractionUtility
    ///     .Build();
    /// </code>
    /// </example>
    public static AgentBuilder WithDocumentHandling(
        this AgentBuilder builder,
        DocumentHandlingOptions? options = null)
    {
        // Get or create shared text extractor instance
        builder._textExtractor ??= new TextExtractionUtility();

        var strategy = new FullTextExtractionStrategy(builder._textExtractor);
        var middleware = new DocumentHandlingMiddleware(strategy, options);

        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Adds document handling middleware with full-text extraction strategy.
    /// Uses the provided TextExtractionUtility instance.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="textExtractor">Text extraction utility instance</param>
    /// <param name="options">Document handling options (optional)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// var customExtractor = new TextExtractionUtility();
    /// var agent = new AgentBuilder()
    ///     .WithChatClient(client)
    ///     .WithDocumentHandling(customExtractor)
    ///     .Build();
    /// </code>
    /// </example>
    public static AgentBuilder WithDocumentHandling(
        this AgentBuilder builder,
        TextExtractionUtility textExtractor,
        DocumentHandlingOptions? options = null)
    {
        if (textExtractor == null)
            throw new ArgumentNullException(nameof(textExtractor));

        // Store the provided extractor for potential reuse
        builder._textExtractor = textExtractor;

        var strategy = new FullTextExtractionStrategy(textExtractor);
        var middleware = new DocumentHandlingMiddleware(strategy, options);

        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Adds document handling middleware with a custom strategy.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="strategy">Custom document processing strategy</param>
    /// <param name="options">Document handling options (optional)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// var agent = new AgentBuilder()
    ///     .WithChatClient(client)
    ///     .WithDocumentHandling(
    ///         new CustomDocumentStrategy(),
    ///         new DocumentHandlingOptions { MaxDocumentSizeBytes = 5 * 1024 * 1024 })
    ///     .Build();
    /// </code>
    /// </example>
    public static AgentBuilder WithDocumentHandling(
        this AgentBuilder builder,
        IDocumentStrategy strategy,
        DocumentHandlingOptions? options = null)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));

        var middleware = new DocumentHandlingMiddleware(strategy, options);
        return builder.WithMiddleware(middleware);
    }
}
