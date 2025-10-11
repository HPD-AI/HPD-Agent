using Microsoft.Extensions.AI;

/// <summary>
/// Example filter demonstrating post-invocation document usage tracking.
/// This filter analyzes which project documents were useful in the conversation
/// and updates relevance scores for better retrieval in the future.
/// </summary>
public class ExampleDocumentUsageFilter : IPromptFilter
{
    private readonly Dictionary<string, int> _documentUsageScores = new();
    private List<string>? _injectedDocumentNames;

    // Pre-processing: Track which documents we injected
    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptFilterContext context,
        Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next)
    {
        // Before calling next, extract document names from project (if available)
        if (context.Properties.TryGetValue("Project", out var projObj) && projObj is Project project)
        {
            var documents = await project.DocumentManager.GetDocumentsAsync();
            _injectedDocumentNames = documents.Select(d => d.FileName).ToList();
        }

        return await next(context);
    }

    // Post-processing: Analyze which documents were referenced in the response
    public Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        // Only analyze successful responses
        if (!context.IsSuccess || context.ResponseMessages == null || _injectedDocumentNames == null)
        {
            return Task.CompletedTask;
        }

        // Extract assistant response text
        var assistantText = string.Join(" ", context.ResponseMessages
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => m.Text ?? ""));

        // Analyze which documents were mentioned or referenced
        foreach (var docName in _injectedDocumentNames)
        {
            // Simple heuristic: Check if document name appears in response
            // More sophisticated: Use semantic similarity, citation analysis, etc.
            if (assistantText.Contains(docName, StringComparison.OrdinalIgnoreCase))
            {
                // Increment usage score
                if (!_documentUsageScores.ContainsKey(docName))
                {
                    _documentUsageScores[docName] = 0;
                }
                _documentUsageScores[docName]++;

                Console.WriteLine($"[DocumentUsage] '{docName}' was referenced (score: {_documentUsageScores[docName]})");
            }
        }

        // In a real implementation, you might:
        // - Update document rankings in a vector database
        // - Store usage metadata for analytics
        // - Adjust future document selection based on relevance
        // - Track which documents were NOT useful (injected but not referenced)

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current usage scores for analysis.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetUsageScores() => _documentUsageScores;
}
