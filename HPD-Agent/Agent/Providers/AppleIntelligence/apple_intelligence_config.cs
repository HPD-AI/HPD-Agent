/// <summary>
/// Configuration for Apple Intelligence chat client and services
/// Based on actual Apple Foundation Models API research
/// </summary>
public sealed class AppleIntelligenceConfig
{
    /// <summary>
    /// The model identifier to use. Since Apple Intelligence uses the system model,
    /// this is primarily for metadata purposes.
    /// </summary>
    public string ModelId { get; set; } = "apple-intelligence";
    
    /// <summary>
    /// The use case for the system language model.
    /// Matches Apple's SystemLanguageModel(useCase:) API
    /// </summary>
    public AppleIntelligenceUseCase UseCase { get; set; } = AppleIntelligenceUseCase.General;
    
    /// <summary>
    /// System instructions to provide to the model.
    /// Matches Apple's LanguageModelSession(instructions:) API
    /// </summary>
    public string? SystemInstructions { get; set; }
    
    /// <summary>
    /// Whether to validate that Apple Intelligence is available before creating sessions.
    /// Default is true for safety.
    /// </summary>
    public bool ValidateAvailability { get; set; } = true;
    
    /// <summary>
    /// Whether to prewarm the model session for better performance.
    /// Matches Apple's session.prewarm() API
    /// Default is true for better user experience.
    /// </summary>
    public bool PrewarmSession { get; set; } = true;
    
    /// <summary>
    /// Maximum number of tokens to keep in the session transcript before truncating.
    /// Helps manage memory usage for long conversations.
    /// Based on Apple's context window management
    /// </summary>
    public int? MaxTranscriptTokens { get; set; } = 3000;
    
    /// <summary>
    /// Default generation options for requests.
    /// Matches Apple's GenerationOptions API
    /// </summary>
    public AppleIntelligenceGenerationOptions? DefaultGenerationOptions { get; set; }

    // ===== SPEECH-RELATED PROPERTIES =====
    
    /// <summary>
    /// Default language for speech recognition and synthesis.
    /// </summary>
    public string? DefaultLanguage { get; set; } = "en-US";
    
    /// <summary>
    /// Default voice for speech synthesis.
    /// </summary>
    public string? DefaultVoice { get; set; } = "com.apple.voice.compact.en-US.Samantha";
    
    /// <summary>
    /// Default speech recognition model preset.
    /// </summary>
    public string? DefaultSpeechModel { get; set; } = "apple-speechanalyzer-v1";
}

/// <summary>
/// Use cases for Apple Intelligence system language models
/// Based on Apple's actual SystemLanguageModel.UseCase enum
/// </summary>
public enum AppleIntelligenceUseCase
{
    /// <summary>
    /// General purpose language model for most use cases
    /// </summary>
    General,
    
    /// <summary>
    /// Specialized model for content tagging, entity extraction, and topic detection
    /// Provides first-class support for tag generation
    /// </summary>
    ContentTagging
}

/// <summary>
/// Generation options specific to Apple Intelligence models
/// Matches Apple's GenerationOptions API structure
/// </summary>
public sealed class AppleIntelligenceGenerationOptions
{
    /// <summary>
    /// Temperature for response generation (0.0 to 2.0).
    /// Lower values make output more focused and deterministic.
    /// Higher values make output more creative and varied.
    /// Matches Apple's GenerationOptions.temperature
    /// </summary>
    public double? Temperature { get; set; }
    
    /// <summary>
    /// Maximum number of tokens to generate in the response.
    /// Matches Apple's GenerationOptions.maximumResponseTokens
    /// </summary>
    public int? MaxTokens { get; set; }
    
    /// <summary>
    /// Sampling method to use for token generation.
    /// Matches Apple's GenerationOptions.sampling
    /// </summary>
    public AppleIntelligenceSamplingMethod? SamplingMethod { get; set; }
    
    /// <summary>
    /// Whether to use greedy sampling (deterministic output).
    /// When true, overrides SamplingMethod and Temperature.
    /// Provides a convenient way to get deterministic output
    /// </summary>
    public bool? UseGreedySampling { get; set; }
}

/// <summary>
/// Sampling methods supported by Apple Intelligence
/// Based on Apple's GenerationOptions.sampling API
/// </summary>
public enum AppleIntelligenceSamplingMethod
{
    /// <summary>
    /// Random sampling with temperature control (default)
    /// </summary>
    Random,
    
    /// <summary>
    /// Greedy sampling (always pick most likely token)
    /// Provides deterministic output
    /// </summary>
    Greedy
}