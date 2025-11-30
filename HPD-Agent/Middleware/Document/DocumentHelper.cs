using HPD_Agent.TextExtraction;

namespace HPD.Agent.Middleware.Document;

/// <summary>
/// Static helper for processing document uploads.
/// Provides utilities for extracting text from documents and formatting messages.
/// </summary>
public static class DocumentHelper
{
    /// <summary>Default document tag format for message injection</summary>
    public const string DefaultDocumentTagFormat = "\n\n[ATTACHED_DOCUMENT[{0}]]\n{1}\n[/ATTACHED_DOCUMENT]\n\n";

    /// <summary>
    /// Process a single document upload.
    /// </summary>
    /// <param name="filePath">Path to file or URL to process</param>
    /// <param name="extractor">TextExtractionUtility instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with extracted text or error</returns>
    public static async Task<DocumentUpload> ProcessUploadAsync(
        string filePath,
        TextExtractionUtility extractor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await extractor.ExtractTextAsync(filePath, cancellationToken);

            return new DocumentUpload
            {
                FileName = result.FileName,
                ExtractedText = result.ExtractedText ?? string.Empty,
                MimeType = result.MimeType,
                FileSize = result.FileSizeBytes,
                ProcessedAt = DateTime.UtcNow,
                Success = result.IsSuccess,
                ErrorMessage = result.ErrorMessage,
                DecoderUsed = "TextExtractionUtility"
            };
        }
        catch (Exception ex)
        {
            return new DocumentUpload
            {
                FileName = System.IO.Path.GetFileName(filePath),
                ExtractedText = string.Empty,
                MimeType = string.Empty,
                FileSize = 0,
                ProcessedAt = DateTime.UtcNow,
                Success = false,
                ErrorMessage = $"Failed to process upload: {ex.Message}",
                DecoderUsed = null
            };
        }
    }

    /// <summary>
    /// Process multiple document uploads.
    /// </summary>
    /// <param name="filePaths">Paths to files or URLs to process</param>
    /// <param name="extractor">TextExtractionUtility instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of upload results</returns>
    public static async Task<DocumentUpload[]> ProcessUploadsAsync(
        string[] filePaths,
        TextExtractionUtility extractor,
        CancellationToken cancellationToken = default)
    {
        if (filePaths == null || filePaths.Length == 0)
            return Array.Empty<DocumentUpload>();

        var tasks = filePaths.Select(path => ProcessUploadAsync(path, extractor, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Format user message with document uploads appended.
    /// </summary>
    /// <param name="userMessage">Original user message</param>
    /// <param name="uploads">Processed document uploads</param>
    /// <param name="documentTagFormat">Custom tag format (optional)</param>
    /// <returns>Formatted message with documents appended</returns>
    public static string FormatMessageWithDocuments(
        string userMessage,
        DocumentUpload[] uploads,
        string? documentTagFormat = null)
    {
        if (uploads == null || uploads.Length == 0)
            return userMessage;

        var format = documentTagFormat ?? DefaultDocumentTagFormat;
        var successfulUploads = uploads.Where(u => u.Success && !string.IsNullOrEmpty(u.ExtractedText)).ToArray();

        if (successfulUploads.Length == 0)
            return userMessage;

        var formattedMessage = userMessage;

        foreach (var upload in successfulUploads)
        {
            formattedMessage += string.Format(format, upload.FileName, upload.ExtractedText);
        }

        return formattedMessage;
    }

    /// <summary>
    /// Create a formatted error message for failed uploads.
    /// </summary>
    /// <param name="failedUploads">Uploads that failed processing</param>
    /// <returns>User-friendly error message</returns>
    public static string FormatUploadErrors(DocumentUpload[] failedUploads)
    {
        if (failedUploads == null || failedUploads.Length == 0)
            return string.Empty;

        var errors = failedUploads
            .Where(u => !u.Success)
            .Select(u => $"• {u.FileName}: {u.ErrorMessage}")
            .ToArray();

        if (errors.Length == 0)
            return string.Empty;

        return $"Failed to process {errors.Length} document(s):\n" + string.Join("\n", errors);
    }
}
