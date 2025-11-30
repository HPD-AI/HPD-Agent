using HPD.Agent.Middleware;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Strategy for processing and injecting documents into agent context.
/// Implementations define how documents are extracted, processed, and added to messages.
/// </summary>
public interface IDocumentStrategy
{
    /// <summary>
    /// Process documents and modify agent context accordingly.
    /// </summary>
    /// <param name="context">Agent middleware context to modify</param>
    /// <param name="documentPaths">Paths to documents to process</param>
    /// <param name="options">Document handling options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ProcessDocumentsAsync(
        AgentMiddlewareContext context,
        IEnumerable<string> documentPaths,
        DocumentHandlingOptions options,
        CancellationToken cancellationToken);
}
