/// <summary>
/// Text-to-Speech client interface following Extensions.AI patterns
/// Will be replaced when Extensions.AI releases official interface
/// </summary>
/// [Experimental("HPDAUDIO001")]
public interface ITextToSpeechClient : IDisposable
{
    /// <summary>Converts text to audio speech.</summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <param name="options">The text to speech options to configure the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The audio speech generated.</returns>
    Task<TextToSpeechResponse> GetAudioAsync(
        string text, 
        TextToSpeechOptions? options = null, 
        CancellationToken cancellationToken = default);
        
    /// <summary>Converts text to audio speech and streams back the generated audio.</summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <param name="options">The text to speech options to configure the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The audio updates representing the streamed output.</returns>
    IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, 
        TextToSpeechOptions? options = null, 
        CancellationToken cancellationToken = default);
        
    /// <summary>Asks the <see cref="ITextToSpeechClient"/> for an object of the specified type <paramref name="serviceType"/>.</summary>
    /// <param name="serviceType">The type of object being requested.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    object? GetService(Type serviceType, object? serviceKey = null);
}
