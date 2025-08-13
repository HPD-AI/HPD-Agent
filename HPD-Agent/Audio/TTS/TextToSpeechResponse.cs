using Microsoft.Extensions.AI;


/// <summary>
/// Text-to-Speech response following Extensions.AI patterns
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class TextToSpeechResponse
{
    /// <summary>Initializes a new instance of the <see cref="TextToSpeechResponse"/> class.</summary>
    /// <param name="audioStream">The audio stream containing the generated speech.</param>
    /// <exception cref="ArgumentNullException"><paramref name="audioStream"/> is <see langword="null"/>.</exception>
    public TextToSpeechResponse(Stream audioStream)
    {
        AudioStream = audioStream ?? throw new ArgumentNullException(nameof(audioStream));
    }

    /// <summary>Gets the audio stream containing the generated speech.</summary>
    public Stream AudioStream { get; }

    /// <summary>Gets or sets the ID of the text to speech response.</summary>
    public string? ResponseId { get; set; }

    /// <summary>Gets or sets the model ID used in the creation of the text to speech response.</summary>
    public string? ModelId { get; set; }

    /// <summary>Gets or sets the voice used in the creation of the text to speech response.</summary>
    public string? Voice { get; set; }

    /// <summary>Gets or sets any additional properties associated with the text to speech response.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

    /// <summary>Gets or sets the raw representation of the text to speech response from an underlying implementation.</summary>
    /// <remarks>
    /// If a <see cref="TextToSpeechResponse"/> is created to represent some underlying object from another object
    /// model, this property can be used to store that original object. This can be useful for debugging or
    /// for enabling a consumer to access the underlying object model if needed.
    /// </remarks>
    public object? RawRepresentation { get; set; }
}
