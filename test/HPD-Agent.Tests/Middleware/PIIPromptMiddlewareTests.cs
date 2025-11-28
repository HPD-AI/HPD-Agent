using HPD.Agent;
using Microsoft.Extensions.AI;
using Xunit;

namespace HPD_Agent.Tests.Middleware;

/// <summary>
/// Tests for PIIPromptMiddleware - PII detection and handling.
/// Covers all strategies (Block, Redact, Mask, Hash), all PII types,
/// and edge cases like Luhn validation and custom detectors.
/// </summary>
public class PIIPromptMiddlewareTests
{
    // ═══════════════════════════════════════════════════════
    // EMAIL DETECTION TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DetectsEmail_WithRedactStrategy_ReplacesWithPlaceholder()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            EmailStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Contact me at john.doe@example.com")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single();
        Assert.Equal("[EMAIL_REDACTED]", processed.Text?.Replace("Contact me at ", ""));
    }

    [Fact]
    public async Task DetectsEmail_WithMaskStrategy_PartiallyMasks()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            EmailStrategy = PIIStrategy.Mask
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Email: john@example.com")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single();
        // Should show first char, mask middle, keep @domain
        Assert.Contains("j***@example.com", processed.Text);
    }

    [Fact]
    public async Task DetectsEmail_WithHashStrategy_CreatesDeterministicHash()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            EmailStrategy = PIIStrategy.Hash
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Email: test@test.com")
        };

        var context = CreateContext(messages);

        // Act - run twice to verify determinism
        var result1 = await middleware.InvokeAsync(context, PassThrough);
        context = CreateContext(new List<ChatMessage> { new(ChatRole.User, "Email: test@test.com") });
        var result2 = await middleware.InvokeAsync(context, PassThrough);

        // Assert - same email should produce same hash
        var hash1 = result1.Single().Text;
        var hash2 = result2.Single().Text;
        Assert.Equal(hash1, hash2);
        Assert.Contains("<email_hash:", hash1);
    }

    [Fact]
    public async Task DetectsMultipleEmails_ReplacesAll()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            EmailStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "CC: a@b.com and b@c.com")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.DoesNotContain("@", processed);
        Assert.Equal(2, CountOccurrences(processed!, "[EMAIL_REDACTED]"));
    }

    // ═══════════════════════════════════════════════════════
    // CREDIT CARD DETECTION TESTS (WITH LUHN VALIDATION)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DetectsCreditCard_ValidLuhn_BlocksMessage()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            CreditCardStrategy = PIIStrategy.Block
        };

        // 4111111111111111 is a valid Luhn test card number
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "My card is 4111111111111111")
        };

        var context = CreateContext(messages);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PIIBlockedException>(
            () => middleware.InvokeAsync(context, PassThrough));

        Assert.Equal("CreditCard", ex.PIIType);
    }

    [Fact]
    public async Task DetectsCreditCard_ValidLuhn_WithMaskStrategy()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            CreditCardStrategy = PIIStrategy.Mask
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Card: 4111111111111111")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("****-****-****-1111", processed);
    }

    [Fact]
    public async Task IgnoresInvalidLuhnNumber_DoesNotTreatAsCreditCard()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            CreditCardStrategy = PIIStrategy.Block
        };

        // 1234567890123456 is NOT a valid Luhn number
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Reference: 1234567890123456")
        };

        var context = CreateContext(messages);

        // Act - should NOT throw since Luhn fails
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("1234567890123456", processed);
    }

    [Fact]
    public async Task DetectsCreditCard_WithDashes_ValidatesAndDetects()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            CreditCardStrategy = PIIStrategy.Redact
        };

        // Same valid card with dashes
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Card: 4111-1111-1111-1111")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("[CREDIT_CARD_REDACTED]", processed);
    }

    // ═══════════════════════════════════════════════════════
    // SSN DETECTION TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DetectsSSN_BlocksByDefault()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware(); // SSN is Block by default

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "My SSN is 123-45-6789")
        };

        var context = CreateContext(messages);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PIIBlockedException>(
            () => middleware.InvokeAsync(context, PassThrough));

        Assert.Equal("SSN", ex.PIIType);
    }

    [Fact]
    public async Task DetectsSSN_WithMaskStrategy_ShowsLastFour()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            SSNStrategy = PIIStrategy.Mask
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "SSN: 123-45-6789")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("***-**-6789", processed);
    }

    // ═══════════════════════════════════════════════════════
    // PHONE NUMBER DETECTION TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DetectsPhone_WithMaskStrategy_ShowsLastFour()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            PhoneStrategy = PIIStrategy.Mask
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Call me at 555-123-4567")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("***-***-4567", processed);
    }

    [Theory]
    [InlineData("555-123-4567")]
    [InlineData("(555) 123-4567")]
    [InlineData("555.123.4567")]
    [InlineData("+1-555-123-4567")]
    public async Task DetectsPhone_VariousFormats(string phone)
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            PhoneStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, $"Phone: {phone}")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("[PHONE_REDACTED]", processed);
    }

    // ═══════════════════════════════════════════════════════
    // IP ADDRESS DETECTION TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DetectsIPAddress_WithHashStrategy()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            IPAddressStrategy = PIIStrategy.Hash
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Server IP: 192.168.1.100")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("<ipaddress_hash:", processed);
        Assert.DoesNotContain("192.168.1.100", processed);
    }

    [Fact]
    public async Task DetectsIPAddress_WithMaskStrategy_ShowsLastOctet()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            IPAddressStrategy = PIIStrategy.Mask
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "IP: 10.0.0.42")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("***.***.***.42", processed);
    }

    // ═══════════════════════════════════════════════════════
    // CUSTOM DETECTOR TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CustomDetector_DetectsPatternAndAppliesStrategy()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware();
        middleware.AddCustomDetector(
            name: "EmployeeId",
            pattern: @"EMP-\d{6}",
            strategy: PIIStrategy.Redact);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Employee EMP-123456 needs access")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text;
        Assert.Contains("[EMPLOYEEID_REDACTED]", processed);
        Assert.DoesNotContain("EMP-123456", processed);
    }

    [Fact]
    public async Task CustomDetector_WithBlockStrategy_ThrowsException()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware();
        middleware.AddCustomDetector(
            name: "SecretCode",
            pattern: @"SECRET-\w+",
            strategy: PIIStrategy.Block);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "The code is SECRET-ABC123")
        };

        var context = CreateContext(messages);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PIIBlockedException>(
            () => middleware.InvokeAsync(context, PassThrough));

        Assert.Equal("SecretCode", ex.PIIType);
    }

    // ═══════════════════════════════════════════════════════
    // SCOPE TESTS (INPUT/OUTPUT/TOOL RESULTS)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyToInput_True_ProcessesUserMessages()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            ApplyToInput = true,
            EmailStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Email: test@test.com"),
            new(ChatRole.Assistant, "I'll help with test@test.com")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var resultList = result.ToList();
        Assert.Contains("[EMAIL_REDACTED]", resultList[0].Text);
        Assert.Contains("test@test.com", resultList[1].Text); // Assistant not processed
    }

    [Fact]
    public async Task ApplyToOutput_True_ProcessesAssistantMessages()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            ApplyToInput = false,
            ApplyToOutput = true,
            EmailStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Email: test@test.com"),
            new(ChatRole.Assistant, "Found: assistant@example.com")
        };

        var context = CreateContext(messages);

        // Simulate next() returning assistant messages
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> nextWithOutput = ctx =>
            Task.FromResult<IEnumerable<ChatMessage>>(new List<ChatMessage>
            {
                new(ChatRole.Assistant, "Found: assistant@example.com")
            });

        // Act
        var result = await middleware.InvokeAsync(context, nextWithOutput);

        // Assert
        var output = result.Single();
        Assert.Contains("[EMAIL_REDACTED]", output.Text);
    }

    // ═══════════════════════════════════════════════════════
    // EDGE CASES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task EmptyMessage_PassesThrough()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        Assert.Single(result);
        Assert.Equal("", result.Single().Text);
    }

    [Fact]
    public async Task NoPII_PassesThroughUnchanged()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello, how are you today?")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        Assert.Equal("Hello, how are you today?", result.Single().Text);
    }

    [Fact]
    public async Task MultiplePIITypes_InSameMessage_HandlesAll()
    {
        // Arrange
        var middleware = new PIIPromptMiddleware
        {
            EmailStrategy = PIIStrategy.Redact,
            PhoneStrategy = PIIStrategy.Redact,
            SSNStrategy = PIIStrategy.Redact
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Email: a@b.com, Phone: 555-123-4567, SSN: 123-45-6789")
        };

        var context = CreateContext(messages);

        // Act
        var result = await middleware.InvokeAsync(context, PassThrough);

        // Assert
        var processed = result.Single().Text!;
        Assert.Contains("[EMAIL_REDACTED]", processed);
        Assert.Contains("[PHONE_REDACTED]", processed);
        Assert.Contains("[SSN_REDACTED]", processed);
    }

    [Fact]
    public void DefaultConfiguration_UsesReasonableDefaults()
    {
        // Arrange & Act
        var middleware = new PIIPromptMiddleware();

        // Assert
        Assert.Equal(PIIStrategy.Redact, middleware.EmailStrategy);
        Assert.Equal(PIIStrategy.Block, middleware.CreditCardStrategy);
        Assert.Equal(PIIStrategy.Block, middleware.SSNStrategy);
        Assert.Equal(PIIStrategy.Mask, middleware.PhoneStrategy);
        Assert.Equal(PIIStrategy.Hash, middleware.IPAddressStrategy);
        Assert.True(middleware.ApplyToInput);
        Assert.False(middleware.ApplyToOutput);
        Assert.False(middleware.ApplyToToolResults);
    }

    // ═══════════════════════════════════════════════════════
    // LUHN ALGORITHM VALIDATION TESTS
    // ═══════════════════════════════════════════════════════

    [Theory]
    [InlineData("4111111111111111", true)]   // Visa test card
    [InlineData("5500000000000004", true)]   // Mastercard test card
    [InlineData("340000000000009", true)]    // Amex test card
    [InlineData("1234567890123456", false)]  // Invalid
    [InlineData("0000000000000000", true)]   // All zeros (valid Luhn)
    [InlineData("1111111111111111", false)]  // All ones (invalid Luhn)
    public void LuhnValidation_CorrectlyValidates(string number, bool expectedValid)
    {
        // Use reflection to test the private method
        var middleware = new PIIPromptMiddleware
        {
            CreditCardStrategy = PIIStrategy.Redact
        };

        // Detect in a message to trigger Luhn validation
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, $"Number: {number}")
        };

        var context = CreateContext(messages);

        // Act
        var result = middleware.InvokeAsync(context, PassThrough).Result;

        // Assert
        var processed = result.Single().Text!;
        if (expectedValid)
        {
            Assert.Contains("[CREDIT_CARD_REDACTED]", processed);
        }
        else
        {
            Assert.Contains(number, processed);
        }
    }

    // ═══════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════

    private static PromptMiddlewareContext CreateContext(IEnumerable<ChatMessage> messages)
    {
        return new PromptMiddlewareContext(
            messages: messages,
            options: null,
            agentName: "TestAgent",
            cancellationToken: CancellationToken.None);
    }

    private static Task<IEnumerable<ChatMessage>> PassThrough(PromptMiddlewareContext context)
    {
        return Task.FromResult(context.Messages);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
