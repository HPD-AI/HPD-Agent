using HPD.Agent.Middleware;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Middleware for handling document attachments in user messages.
/// Extracts, processes, and injects document content based on strategy.
///
/// Usage:
/// <code>
/// var agent = new AgentBuilder()
///     .WithMiddleware(new DocumentHandlingMiddleware(
///         new FullTextExtractionStrategy(textExtractor),
///         new DocumentHandlingOptions { CustomTagFormat = "[DOC[{0}]]" }
///     ))
///     .Build();
/// </code>
/// </summary>
public class DocumentHandlingMiddleware : IAgentMiddleware
{
    private readonly IDocumentStrategy _strategy;
    private readonly DocumentHandlingOptions _options;

    /// <summary>
    /// Creates a new DocumentHandlingMiddleware with the specified strategy and options.
    /// </summary>
    /// <param name="strategy">Strategy for processing documents</param>
    /// <param name="options">Document handling options (optional)</param>
    public DocumentHandlingMiddleware(
        IDocumentStrategy strategy,
        DocumentHandlingOptions? options = null)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _options = options ?? new DocumentHandlingOptions();
    }

    /// <summary>
    /// Called before processing a user message turn.
    /// Checks for document paths in context and processes them if present.
    /// </summary>
    public async Task BeforeMessageTurnAsync(
        AgentMiddlewareContext context,
        CancellationToken cancellationToken)
    {
        // Extract document paths from context
        var documentPaths = ExtractDocumentPaths(context);
        if (!documentPaths.Any())
            return;

        // Process documents using strategy
        await _strategy.ProcessDocumentsAsync(
            context,
            documentPaths,
            _options,
            cancellationToken);
    }

    /// <summary>
    /// Extract document paths from the agent middleware context.
    /// Uses ChatMessageDocumentExtensions.GetDocumentPaths() to retrieve attached paths.
    /// </summary>
    private IEnumerable<string> ExtractDocumentPaths(AgentMiddlewareContext context)
    {
        return context.UserMessage?.GetDocumentPaths() ?? Enumerable.Empty<string>();
    }
}
