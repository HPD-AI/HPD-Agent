using Microsoft.Extensions.AI;

/// <summary>
/// Represents the result of a streaming turn, providing both the stream and the final turn history
/// </summary>
public class StreamingTurnResult
{
    /// <summary>
    /// The stream of response updates that can be consumed by the caller
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> ResponseStream { get; }

    /// <summary>
    /// Task that completes with the final turn history once streaming is done
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> FinalHistory { get; }

    /// <summary>
    /// Initializes a new instance of StreamingTurnResult
    /// </summary>
    /// <param name="responseStream">The stream of response updates</param>
    /// <param name="finalHistory">Task that provides the final turn history</param>
    public StreamingTurnResult(
        IAsyncEnumerable<ChatResponseUpdate> responseStream,
        Task<IReadOnlyList<ChatMessage>> finalHistory)
    {
        ResponseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        FinalHistory = finalHistory ?? throw new ArgumentNullException(nameof(finalHistory));
    }
}