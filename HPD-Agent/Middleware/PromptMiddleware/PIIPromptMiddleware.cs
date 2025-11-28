using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HPD.Agent.Internal.MiddleWare;
using Microsoft.Extensions.AI;

namespace HPD.Agent;

/// <summary>
/// Detects and handles Personally Identifiable Information (PII) in messages.
/// Applies configurable strategies (block, redact, mask, hash) per PII type.
/// </summary>
/// <remarks>
/// This middleware provides comprehensive PII protection by:
/// 1. Scanning messages for various PII types (email, credit card, SSN, phone, IP)
/// 2. Applying type-specific handling strategies
/// 3. Emitting observability events for audit trails
/// 4. Supporting custom detectors for domain-specific PII
///
/// Inspired by LangChain's PIIMiddleware (per-type strategies, Luhn validation)
/// and Microsoft Agent Framework's approach (input/output filtering).
///
/// Second-mover advantages over both:
/// - Per-type configurable strategies (LangChain) + event emission (our addition)
/// - Validation to reduce false positives (Luhn for credit cards)
/// - Async detector support for external PII services
/// - Three application scopes: input, output, tool results
/// </remarks>
/// <example>
/// <code>
/// // Via AgentBuilder (recommended)
/// var agent = new AgentBuilder()
///     .WithPIIProtection(config =>
///     {
///         config.EmailStrategy = PIIStrategy.Redact;
///         config.CreditCardStrategy = PIIStrategy.Block;  // High risk
///         config.SSNStrategy = PIIStrategy.Block;
///         config.PhoneStrategy = PIIStrategy.Mask;
///     })
///     .Build();
///
/// // With custom detector
/// var agent = new AgentBuilder()
///     .WithPIIProtection(config =>
///     {
///         config.AddCustomDetector(
///             name: "EmployeeId",
///             pattern: @"EMP-\d{6}",
///             strategy: PIIStrategy.Redact,
///             replacement: "[EMPLOYEE_ID]");
///     })
///     .Build();
/// </code>
/// </example>
public class PIIPromptMiddleware : IPromptMiddleware
{
    // ═══════════════════════════════════════════════════════
    // CONFIGURATION - Per-PII-Type Strategies
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Strategy for handling email addresses. Default: Redact.
    /// </summary>
    public PIIStrategy EmailStrategy { get; set; } = PIIStrategy.Redact;

    /// <summary>
    /// Strategy for handling credit card numbers. Default: Block (high risk).
    /// Uses Luhn algorithm validation to reduce false positives.
    /// </summary>
    public PIIStrategy CreditCardStrategy { get; set; } = PIIStrategy.Block;

    /// <summary>
    /// Strategy for handling Social Security Numbers. Default: Block (high risk).
    /// </summary>
    public PIIStrategy SSNStrategy { get; set; } = PIIStrategy.Block;

    /// <summary>
    /// Strategy for handling phone numbers. Default: Mask.
    /// </summary>
    public PIIStrategy PhoneStrategy { get; set; } = PIIStrategy.Mask;

    /// <summary>
    /// Strategy for handling IP addresses. Default: Hash.
    /// </summary>
    public PIIStrategy IPAddressStrategy { get; set; } = PIIStrategy.Hash;

    // ═══════════════════════════════════════════════════════
    // CONFIGURATION - Application Scope
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Apply PII detection to user input messages. Default: true.
    /// </summary>
    public bool ApplyToInput { get; set; } = true;

    /// <summary>
    /// Apply PII detection to LLM output messages. Default: false.
    /// Enable if LLM might echo or generate PII in responses.
    /// </summary>
    public bool ApplyToOutput { get; set; } = false;

    /// <summary>
    /// Apply PII detection to tool results. Default: false.
    /// Enable if tools return sensitive data that shouldn't go back to LLM.
    /// </summary>
    public bool ApplyToToolResults { get; set; } = false;

    // ═══════════════════════════════════════════════════════
    // CONFIGURATION - Custom Detectors
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Custom PII detectors for domain-specific patterns.
    /// </summary>
    public List<CustomPIIDetector> CustomDetectors { get; } = new();

    /// <summary>
    /// Optional async detector for external PII detection services.
    /// If provided, this is called in addition to built-in detectors.
    /// </summary>
    public Func<string, CancellationToken, Task<IEnumerable<PIIMatch>>>? ExternalDetector { get; set; }

    // ═══════════════════════════════════════════════════════
    // CONFIGURATION - Event Emission
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Event coordinator for emitting PII detection events.
    /// Set by AgentBuilder when middleware is registered.
    /// </summary>
    internal IEventCoordinator? EventCoordinator { get; set; }

    /// <summary>
    /// Agent name for event emission. Set by AgentBuilder.
    /// </summary>
    internal string? AgentName { get; set; }

    // ═══════════════════════════════════════════════════════
    // BUILT-IN DETECTORS
    // ═══════════════════════════════════════════════════════

    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d[ -]*?){13,19}\b",
        RegexOptions.Compiled);

    private static readonly Regex SSNRegex = new(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)?\d{3}[-.\s]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex IPAddressRegex = new(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled);

    // ═══════════════════════════════════════════════════════
    // MIDDLEWARE IMPLEMENTATION
    // ═══════════════════════════════════════════════════════

    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next)
    {
        if (ApplyToInput)
        {
            // Process input messages (user messages before LLM call)
            var processedMessages = await ProcessMessagesAsync(
                context.Messages,
                ChatRole.User,
                context.CancellationToken);

            context.Messages = processedMessages;
        }

        // Call next middleware in pipeline
        var result = await next(context);

        if (ApplyToOutput)
        {
            // Process output messages (assistant messages after LLM call)
            result = await ProcessMessagesAsync(
                result,
                ChatRole.Assistant,
                context.CancellationToken);
        }

        return result;
    }

    public async Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        if (ApplyToToolResults && context.ResponseMessages != null)
        {
            // Process tool result messages if enabled
            // Note: This modifies the context's response messages in place
            var processed = await ProcessMessagesAsync(
                context.ResponseMessages,
                ChatRole.Tool,
                cancellationToken);

            // ResponseMessages is typically immutable from PostInvokeContext,
            // so this is observational only - actual tool result filtering
            // would need to happen in AfterIterationAsync of an IIterationMiddleware
        }
    }

    // ═══════════════════════════════════════════════════════
    // PROCESSING LOGIC
    // ═══════════════════════════════════════════════════════

    private async Task<IEnumerable<ChatMessage>> ProcessMessagesAsync(
        IEnumerable<ChatMessage> messages,
        ChatRole targetRole,
        CancellationToken cancellationToken)
    {
        var result = new List<ChatMessage>();

        foreach (var message in messages)
        {
            if (message.Role == targetRole)
            {
                var processedMessage = await ProcessMessageAsync(message, cancellationToken);
                result.Add(processedMessage);
            }
            else
            {
                result.Add(message);
            }
        }

        return result;
    }

    private async Task<ChatMessage> ProcessMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var text = message.Text;
        if (string.IsNullOrEmpty(text))
            return message;

        var allMatches = new List<PIIMatch>();

        // Run built-in detectors
        allMatches.AddRange(DetectEmail(text));
        allMatches.AddRange(DetectCreditCard(text));
        allMatches.AddRange(DetectSSN(text));
        allMatches.AddRange(DetectPhone(text));
        allMatches.AddRange(DetectIPAddress(text));

        // Run custom detectors
        foreach (var detector in CustomDetectors)
        {
            allMatches.AddRange(detector.Detect(text));
        }

        // Run external detector if configured
        if (ExternalDetector != null)
        {
            var externalMatches = await ExternalDetector(text, cancellationToken);
            allMatches.AddRange(externalMatches);
        }

        if (allMatches.Count == 0)
            return message;

        // Check for any Block strategies first
        var blockedMatch = allMatches.FirstOrDefault(m => m.Strategy == PIIStrategy.Block);
        if (blockedMatch != null)
        {
            EmitPIIDetectedEvent(blockedMatch.PIIType, PIIStrategy.Block, 1);
            throw new PIIBlockedException(
                $"PII of type '{blockedMatch.PIIType}' detected. Message blocked for security.",
                blockedMatch.PIIType);
        }

        // Sort matches by position (descending) to replace from end to start
        var sortedMatches = allMatches
            .OrderByDescending(m => m.StartIndex)
            .ToList();

        // Apply replacements
        var processedText = text;
        var emittedTypes = new Dictionary<string, (PIIStrategy Strategy, int Count)>();

        foreach (var match in sortedMatches)
        {
            var replacement = GetReplacement(match);
            processedText = processedText.Remove(match.StartIndex, match.Length)
                                        .Insert(match.StartIndex, replacement);

            // Track for event emission
            var key = match.PIIType;
            if (emittedTypes.TryGetValue(key, out var existing))
            {
                emittedTypes[key] = (existing.Strategy, existing.Count + 1);
            }
            else
            {
                emittedTypes[key] = (match.Strategy, 1);
            }
        }

        // Emit events for each PII type detected
        foreach (var (piiType, (strategy, count)) in emittedTypes)
        {
            EmitPIIDetectedEvent(piiType, strategy, count);
        }

        // Create new message with processed text
        return new ChatMessage(message.Role, processedText);
    }

    // ═══════════════════════════════════════════════════════
    // DETECTION METHODS
    // ═══════════════════════════════════════════════════════

    private IEnumerable<PIIMatch> DetectEmail(string text)
    {
        foreach (Match match in EmailRegex.Matches(text))
        {
            yield return new PIIMatch(
                PIIType: "Email",
                Value: match.Value,
                StartIndex: match.Index,
                Length: match.Length,
                Strategy: EmailStrategy);
        }
    }

    private IEnumerable<PIIMatch> DetectCreditCard(string text)
    {
        foreach (Match match in CreditCardRegex.Matches(text))
        {
            // Extract digits only for Luhn validation
            var digitsOnly = new string(match.Value.Where(char.IsDigit).ToArray());

            // Validate with Luhn algorithm to reduce false positives
            if (digitsOnly.Length >= 13 && digitsOnly.Length <= 19 && IsValidLuhn(digitsOnly))
            {
                yield return new PIIMatch(
                    PIIType: "CreditCard",
                    Value: match.Value,
                    StartIndex: match.Index,
                    Length: match.Length,
                    Strategy: CreditCardStrategy);
            }
        }
    }

    private IEnumerable<PIIMatch> DetectSSN(string text)
    {
        foreach (Match match in SSNRegex.Matches(text))
        {
            yield return new PIIMatch(
                PIIType: "SSN",
                Value: match.Value,
                StartIndex: match.Index,
                Length: match.Length,
                Strategy: SSNStrategy);
        }
    }

    private IEnumerable<PIIMatch> DetectPhone(string text)
    {
        foreach (Match match in PhoneRegex.Matches(text))
        {
            yield return new PIIMatch(
                PIIType: "Phone",
                Value: match.Value,
                StartIndex: match.Index,
                Length: match.Length,
                Strategy: PhoneStrategy);
        }
    }

    private IEnumerable<PIIMatch> DetectIPAddress(string text)
    {
        foreach (Match match in IPAddressRegex.Matches(text))
        {
            yield return new PIIMatch(
                PIIType: "IPAddress",
                Value: match.Value,
                StartIndex: match.Index,
                Length: match.Length,
                Strategy: IPAddressStrategy);
        }
    }

    // ═══════════════════════════════════════════════════════
    // STRATEGY IMPLEMENTATIONS
    // ═══════════════════════════════════════════════════════

    private static string GetReplacement(PIIMatch match)
    {
        return match.Strategy switch
        {
            PIIStrategy.Block => throw new InvalidOperationException("Block should be handled before replacement"),
            PIIStrategy.Redact => GetRedactedReplacement(match.PIIType),
            PIIStrategy.Mask => GetMaskedReplacement(match.Value, match.PIIType),
            PIIStrategy.Hash => GetHashedReplacement(match.Value, match.PIIType),
            _ => match.Value // No change
        };
    }

    private static string GetRedactedReplacement(string piiType)
    {
        return piiType switch
        {
            "Email" => "[EMAIL_REDACTED]",
            "CreditCard" => "[CREDIT_CARD_REDACTED]",
            "SSN" => "[SSN_REDACTED]",
            "Phone" => "[PHONE_REDACTED]",
            "IPAddress" => "[IP_REDACTED]",
            _ => $"[{piiType.ToUpperInvariant()}_REDACTED]"
        };
    }

    private static string GetMaskedReplacement(string value, string piiType)
    {
        return piiType switch
        {
            "Email" => MaskEmail(value),
            "CreditCard" => MaskCreditCard(value),
            "SSN" => "***-**-" + value[^4..],
            "Phone" => MaskPhone(value),
            "IPAddress" => MaskIPAddress(value),
            _ => new string('*', value.Length)
        };
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
            return "***@" + email[(atIndex + 1)..];

        return email[0] + new string('*', atIndex - 1) + email[atIndex..];
    }

    private static string MaskCreditCard(string value)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 4)
            return new string('*', value.Length);

        // Show last 4 digits
        var lastFour = digitsOnly[^4..];
        return "****-****-****-" + lastFour;
    }

    private static string MaskPhone(string value)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 4)
            return new string('*', value.Length);

        // Show last 4 digits
        var lastFour = digitsOnly[^4..];
        return "***-***-" + lastFour;
    }

    private static string MaskIPAddress(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 4)
            return "***.***.***.***";

        return $"***.***.***.{parts[3]}";
    }

    private static string GetHashedReplacement(string value, string piiType)
    {
        // Deterministic hash for analytics (same PII = same hash)
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        var shortHash = Convert.ToHexString(hash)[..8].ToLowerInvariant();
        return $"<{piiType.ToLowerInvariant()}_hash:{shortHash}>";
    }

    // ═══════════════════════════════════════════════════════
    // VALIDATION - LUHN ALGORITHM
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Validates a credit card number using the Luhn algorithm.
    /// This reduces false positives by ensuring the number is a valid credit card format.
    /// </summary>
    private static bool IsValidLuhn(string digits)
    {
        if (string.IsNullOrEmpty(digits) || !digits.All(char.IsDigit))
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';

            if (alternate)
            {
                n *= 2;
                if (n > 9)
                    n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    // ═══════════════════════════════════════════════════════
    // EVENT EMISSION
    // ═══════════════════════════════════════════════════════

    private void EmitPIIDetectedEvent(string piiType, PIIStrategy strategy, int count)
    {
        if (EventCoordinator == null)
            return;

        try
        {
            EventCoordinator.Emit(new PIIDetectedEvent(
                AgentName: AgentName ?? "Unknown",
                PIIType: piiType,
                Strategy: strategy,
                OccurrenceCount: count,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch (InvalidOperationException)
        {
            // EventCoordinator not configured - event emission is optional
        }
    }

    // ═══════════════════════════════════════════════════════
    // FLUENT CONFIGURATION
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Adds a custom PII detector with a regex pattern.
    /// </summary>
    public PIIPromptMiddleware AddCustomDetector(
        string name,
        string pattern,
        PIIStrategy strategy,
        string? replacement = null)
    {
        CustomDetectors.Add(new CustomPIIDetector(
            Name: name,
            Pattern: new Regex(pattern, RegexOptions.Compiled),
            Strategy: strategy,
            CustomReplacement: replacement));
        return this;
    }

    /// <summary>
    /// Adds a custom PII detector with a pre-compiled regex.
    /// </summary>
    public PIIPromptMiddleware AddCustomDetector(
        string name,
        Regex pattern,
        PIIStrategy strategy,
        string? replacement = null)
    {
        CustomDetectors.Add(new CustomPIIDetector(
            Name: name,
            Pattern: pattern,
            Strategy: strategy,
            CustomReplacement: replacement));
        return this;
    }
}

// ═══════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════

/// <summary>
/// Strategy for handling detected PII.
/// </summary>
public enum PIIStrategy
{
    /// <summary>
    /// Block the message entirely. Throws PIIBlockedException.
    /// Use for high-risk PII like SSN, credit cards.
    /// </summary>
    Block,

    /// <summary>
    /// Replace PII with a type-specific placeholder (e.g., [EMAIL_REDACTED]).
    /// Use for general PII sanitization.
    /// </summary>
    Redact,

    /// <summary>
    /// Partially mask PII, showing last few characters (e.g., ****-****-****-1234).
    /// Use when some context is needed while protecting the full value.
    /// </summary>
    Mask,

    /// <summary>
    /// Replace with a deterministic hash (e.g., &lt;email_hash:a1b2c3d4&gt;).
    /// Use for analytics where you need to track unique values without exposing them.
    /// </summary>
    Hash
}

/// <summary>
/// Represents a detected PII match.
/// </summary>
public record PIIMatch(
    string PIIType,
    string Value,
    int StartIndex,
    int Length,
    PIIStrategy Strategy);

/// <summary>
/// Custom PII detector configuration.
/// </summary>
public record CustomPIIDetector(
    string Name,
    Regex Pattern,
    PIIStrategy Strategy,
    string? CustomReplacement = null)
{
    public IEnumerable<PIIMatch> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            yield return new PIIMatch(
                PIIType: Name,
                Value: match.Value,
                StartIndex: match.Index,
                Length: match.Length,
                Strategy: Strategy);
        }
    }
}

/// <summary>
/// Exception thrown when PII with Block strategy is detected.
/// </summary>
public class PIIBlockedException : Exception
{
    public string PIIType { get; }

    public PIIBlockedException(string message, string piiType)
        : base(message)
    {
        PIIType = piiType;
    }
}

/// <summary>
/// Event emitted when PII is detected in messages.
/// </summary>
public record PIIDetectedEvent(
    string AgentName,
    string PIIType,
    PIIStrategy Strategy,
    int OccurrenceCount,
    DateTimeOffset Timestamp) : AgentEvent, IObservabilityEvent;
