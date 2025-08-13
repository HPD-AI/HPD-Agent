using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;


/// <summary>
/// Text-to-Speech options following Extensions.AI patterns
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class TextToSpeechOptions
{
    /// <summary>Gets or sets any additional properties associated with the options.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

    /// <summary>Gets or sets the model ID for the text to speech conversion.</summary>
    public string? ModelId { get; set; }

    /// <summary>Gets or sets the voice to use for speech synthesis.</summary>
    public string? Voice { get; set; }

    /// <summary>Gets or sets the language for the generated speech.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the speed/rate of the generated speech.</summary>
    public float? Speed { get; set; }

    /// <summary>Gets or sets the pitch of the generated speech.</summary>
    public float? Pitch { get; set; }

    /// <summary>Gets or sets the output format for the generated audio.</summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets a callback responsible for creating the raw representation of the text to speech options from an underlying implementation.
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="ITextToSpeechClient" /> implementation may have its own representation of options.
    /// When <see cref="ITextToSpeechClient.GetAudioAsync" /> or <see cref="ITextToSpeechClient.GetStreamingAudioAsync"/>
    /// is invoked with a <see cref="TextToSpeechOptions" />, that implementation may convert the provided options into
    /// its own representation in order to use it while performing the operation. For situations where a consumer knows
    /// which concrete <see cref="ITextToSpeechClient" /> is being used and how it represents options, a new instance of that
    /// implementation-specific options type may be returned by this callback, for the <see cref="ITextToSpeechClient" />
    /// implementation to use instead of creating a new instance. Such implementations may mutate the supplied options
    /// instance further based on other settings supplied on this <see cref="TextToSpeechOptions" /> instance or from other inputs,
    /// therefore, it is <b>strongly recommended</b> to not return shared instances and instead make the callback return a new instance on each call.
    /// This is typically used to set an implementation-specific setting that isn't otherwise exposed from the strongly-typed
    /// properties on <see cref="TextToSpeechOptions" />.
    /// </remarks>
    [JsonIgnore]
    public Func<ITextToSpeechClient, object?>? RawRepresentationFactory { get; set; }

    /// <summary>Produces a clone of the current <see cref="TextToSpeechOptions"/> instance.</summary>
    /// <returns>A clone of the current <see cref="TextToSpeechOptions"/> instance.</returns>
    public TextToSpeechOptions Clone()
    {
        var clone = new TextToSpeechOptions
        {
            ModelId = ModelId,
            Voice = Voice,
            Language = Language,
            Speed = Speed,
            Pitch = Pitch,
            OutputFormat = OutputFormat,
            RawRepresentationFactory = RawRepresentationFactory
        };

        if (AdditionalProperties is { Count: > 0 })
        {
            clone.AdditionalProperties = new AdditionalPropertiesDictionary();
            foreach (var kvp in AdditionalProperties)
            {
                clone.AdditionalProperties[kvp.Key] = kvp.Value;
            }
        }

        return clone;
    }
}
