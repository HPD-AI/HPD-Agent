using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

/// <summary>
/// Text-to-Speech streaming update
/// </summary>
[Experimental("HPDAUDIO001")]
public sealed class TextToSpeechResponseUpdate
{
    /// <summary>Initializes a new instance of the <see cref="TextToSpeechResponseUpdate"/> class.</summary>
    /// <param name="audioChunk">The audio chunk for this update.</param>
    /// <exception cref="ArgumentNullException"><paramref name="audioChunk"/> is <see langword="null"/>.</exception>
    public TextToSpeechResponseUpdate(Stream audioChunk)
    {
        AudioChunk = audioChunk ?? throw new ArgumentNullException(nameof(audioChunk));
    }

    /// <summary>Gets the audio chunk for this update.</summary>
    public Stream AudioChunk { get; }

    /// <summary>Gets or sets the kind of update this represents.</summary>
    public TextToSpeechResponseUpdateKind Kind { get; set; } = TextToSpeechResponseUpdateKind.AudioUpdated;

    /// <summary>Gets or sets the model ID used in the creation of this update.</summary>
    public string? ModelId { get; set; }

    /// <summary>Gets or sets the voice used in the creation of this update.</summary>
    public string? Voice { get; set; }

    /// <summary>Gets or sets any additional properties associated with this update.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}

/// <summary>
/// Text-to-Speech response update kinds
/// </summary>
[Experimental("HPDAUDIO001")]
public enum TextToSpeechResponseUpdateKind
{
    /// <summary>Audio generation has started.</summary>
    AudioStarted,

    /// <summary>Audio chunk has been updated/generated.</summary>
    AudioUpdated,

    /// <summary>Audio generation has completed.</summary>
    AudioCompleted,

    /// <summary>An error occurred during audio generation.</summary>
    Error
}
