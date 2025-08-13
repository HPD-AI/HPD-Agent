/// <summary>
/// Represents the availability status of Apple Intelligence
/// Based on Apple's SystemLanguageModel.Availability API
/// </summary>
public readonly struct AppleIntelligenceAvailability
{
    private readonly bool _isAvailable;
    private readonly AppleIntelligenceUnavailableReason _unavailableReason;

    private AppleIntelligenceAvailability(bool isAvailable, AppleIntelligenceUnavailableReason unavailableReason = default)
    {
        _isAvailable = isAvailable;
        _unavailableReason = unavailableReason;
    }

    /// <summary>
    /// Gets whether Apple Intelligence is available
    /// </summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>
    /// Gets the reason why Apple Intelligence is unavailable (only valid when IsAvailable is false)
    /// </summary>
    public AppleIntelligenceUnavailableReason UnavailableReason => _unavailableReason;

    /// <summary>
    /// Creates an available status
    /// </summary>
    public static AppleIntelligenceAvailability Available => new(true);

    /// <summary>
    /// Creates an unavailable status with a reason
    /// </summary>
    public static AppleIntelligenceAvailability Unavailable(AppleIntelligenceUnavailableReason reason) => new(false, reason);
}

/// <summary>
/// Reasons why Apple Intelligence might be unavailable
/// Based on Apple's SystemLanguageModel.Availability.UnavailableReason
/// </summary>
public enum AppleIntelligenceUnavailableReason
{
    /// <summary>
    /// Apple Intelligence is not enabled in system settings
    /// </summary>
    AppleIntelligenceNotEnabled,

    /// <summary>
    /// The device is not eligible for Apple Intelligence (hardware/OS requirements not met)
    /// </summary>
    DeviceNotEligible,

    /// <summary>
    /// The model is not ready (downloading, initializing, etc.)
    /// </summary>
    ModelNotReady,

    /// <summary>
    /// The current system language is not supported by Apple Intelligence
    /// </summary>
    UnsupportedLanguage,

    /// <summary>
    /// Unknown reason
    /// </summary>
    Unknown
}

/// <summary>
/// Exception thrown when Apple Intelligence operations fail
/// Based on research of Apple's error handling patterns
/// </summary>
public sealed class AppleIntelligenceException : Exception
{
    /// <summary>
    /// Gets the Apple Intelligence error code
    /// </summary>
    public AppleIntelligenceErrorCode ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of AppleIntelligenceException
    /// </summary>
    public AppleIntelligenceException(string message, AppleIntelligenceErrorCode errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of AppleIntelligenceException
    /// </summary>
    public AppleIntelligenceException(string message, AppleIntelligenceErrorCode errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}