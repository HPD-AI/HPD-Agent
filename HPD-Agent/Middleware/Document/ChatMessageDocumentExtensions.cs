using Microsoft.Extensions.AI;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Extension methods for attaching document paths to ChatMessages.
/// This allows documents to be processed by DocumentHandlingMiddleware.
/// </summary>
public static class ChatMessageDocumentExtensions
{
    private const string DOCUMENT_PATHS_KEY = "DocumentPaths";

    /// <summary>
    /// Attaches document paths to a ChatMessage for processing by DocumentHandlingMiddleware.
    /// </summary>
    /// <param name="message">The message to attach documents to</param>
    /// <param name="documentPaths">Paths to documents (file paths or URLs)</param>
    /// <returns>The same message for chaining</returns>
    /// <example>
    /// <code>
    /// var message = new ChatMessage(ChatRole.User, "Analyze this document")
    ///     .WithDocuments("report.pdf", "data.xlsx");
    /// </code>
    /// </example>
    public static ChatMessage WithDocuments(this ChatMessage message, params string[] documentPaths)
    {
        if (documentPaths == null || documentPaths.Length == 0)
            return message;

        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[DOCUMENT_PATHS_KEY] = documentPaths;

        return message;
    }

    /// <summary>
    /// Gets document paths attached to a ChatMessage.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>Array of document paths, or empty array if none attached</returns>
    public static string[] GetDocumentPaths(this ChatMessage message)
    {
        if (message.AdditionalProperties?.TryGetValue(DOCUMENT_PATHS_KEY, out var paths) == true)
        {
            if (paths is string[] pathsArray)
                return pathsArray;
            if (paths is IEnumerable<string> pathsEnum)
                return pathsEnum.ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Checks if a ChatMessage has documents attached.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if documents are attached</returns>
    public static bool HasDocuments(this ChatMessage message)
    {
        return message.GetDocumentPaths().Length > 0;
    }
}
