using HPD.Agent.Middleware;
using HPD_Agent.TextExtraction;
using Microsoft.Extensions.AI;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Extracts full text from documents and injects into user message.
/// This is the default document handling strategy (current behavior).
/// Uses DocumentHelper for text extraction and formatting.
/// </summary>
public class FullTextExtractionStrategy : IDocumentStrategy
{
    private readonly TextExtractionUtility _extractor;

    /// <summary>
    /// Creates a new FullTextExtractionStrategy with the specified text extractor.
    /// </summary>
    /// <param name="extractor">Text extraction utility for processing documents</param>
    public FullTextExtractionStrategy(TextExtractionUtility extractor)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
    }

    /// <summary>
    /// Process documents by extracting text and injecting into the last user message.
    /// </summary>
    public async Task ProcessDocumentsAsync(
        AgentMiddlewareContext context,
        IEnumerable<string> documentPaths,
        DocumentHandlingOptions options,
        CancellationToken cancellationToken)
    {
        var paths = documentPaths.ToArray();
        if (paths.Length == 0)
            return;

        // Process uploads using DocumentHelper
        var uploads = await DocumentHelper.ProcessUploadsAsync(
            paths,
            _extractor,
            cancellationToken);

        // Inject into last user message
        InjectDocumentsIntoMessages(context, uploads, options.CustomTagFormat);
    }

    /// <summary>
    /// Inject document content into the last user message in context.
    /// This is the logic extracted from Agent.cs ModifyLastUserMessageWithDocuments.
    /// </summary>
    private void InjectDocumentsIntoMessages(
        AgentMiddlewareContext context,
        DocumentUpload[] uploads,
        string? customTagFormat)
    {
        if (context.Messages == null || !context.Messages.Any() || uploads.Length == 0)
            return;

        var messagesList = context.Messages.ToList();

        // Find the last user message
        var lastUserMessageIndex = -1;
        for (int i = messagesList.Count - 1; i >= 0; i--)
        {
            if (messagesList[i].Role == ChatRole.User)
            {
                lastUserMessageIndex = i;
                break;
            }
        }

        if (lastUserMessageIndex == -1)
            return;

        var lastUserMessage = messagesList[lastUserMessageIndex];
        
        // Extract text from message (handles multiple TextContent items)
        var originalText = string.IsNullOrEmpty(lastUserMessage.Text) 
            ? string.Join(" ", lastUserMessage.Contents
                .OfType<TextContent>()
                .Where(t => !string.IsNullOrEmpty(t.Text))
                .Select(t => t.Text))
            : lastUserMessage.Text;

        // Format message with documents using DocumentHelper
        var formattedMessage = DocumentHelper.FormatMessageWithDocuments(
            originalText, uploads, customTagFormat);

        // Append document content to existing contents instead of replacing
        // This preserves images, audio, and other non-text content
        var newContents = lastUserMessage.Contents.ToList();
        newContents.Add(new TextContent(formattedMessage));

        // Preserve AdditionalProperties if present
        var newMessage = new ChatMessage(ChatRole.User, newContents);
        if (lastUserMessage.AdditionalProperties != null)
        {
            newMessage.AdditionalProperties = new AdditionalPropertiesDictionary(lastUserMessage.AdditionalProperties);
        }

        messagesList[lastUserMessageIndex] = newMessage;

        // Update context messages
        context.Messages = messagesList;
    }

}
