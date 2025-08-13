using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for Apple Intelligence integration
/// </summary>
public static class AppleIntelligenceExtensions
{
    /// <summary>
    /// Adds Apple Intelligence chat client to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="config">Apple Intelligence configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppleIntelligence(this IServiceCollection services, AppleIntelligenceConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // Check availability before registering
        if (config.ValidateAvailability && !AppleIntelligenceChatClient.IsAvailable)
        {
            throw new InvalidOperationException("Apple Intelligence is not available on this system");
        }

        return services.AddSingleton<IChatClient>(provider => new AppleIntelligenceChatClient(config));
    }

    /// <summary>
    /// Adds Apple Intelligence chat client to the service collection with configuration action
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppleIntelligence(this IServiceCollection services, Action<AppleIntelligenceConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new AppleIntelligenceConfig();
        configure(config);

        return services.AddAppleIntelligence(config);
    }

    /// <summary>
    /// Attempts to get the Apple Intelligence chat client from the IChatClient
    /// </summary>
    /// <param name="chatClient">The chat client</param>
    /// <returns>The Apple Intelligence client if available, otherwise null</returns>
    public static AppleIntelligenceChatClient? AsAppleIntelligence(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return chatClient.GetService(typeof(AppleIntelligenceChatClient)) as AppleIntelligenceChatClient;
    }

    /// <summary>
    /// Checks if the chat client is an Apple Intelligence client
    /// </summary>
    /// <param name="chatClient">The chat client</param>
    /// <returns>True if the client is Apple Intelligence, false otherwise</returns>
    public static bool IsAppleIntelligence(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return chatClient.AsAppleIntelligence() != null;
    }

    /// <summary>
    /// Extension method for ChatOptions to set a session key for conversation management
    /// </summary>
    /// <param name="options">The chat options</param>
    /// <param name="sessionKey">The session key to use</param>
    /// <returns>The chat options for chaining</returns>
    public static ChatOptions WithSessionKey(this ChatOptions options, string sessionKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionKey);

        // Store session key in additional properties
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["SessionKey"] = sessionKey;
        return options;
    }

    /// <summary>
    /// Gets the session key from chat options
    /// </summary>
    /// <param name="options">The chat options</param>
    /// <returns>The session key if set, otherwise null</returns>
    public static string? GetSessionKey(this ChatOptions? options)
    {
        return options?.AdditionalProperties?.TryGetValue("SessionKey", out var value) == true 
            ? value as string 
            : null;
    }

    /// <summary>
    /// Extension method for ChatOptions to configure Apple Intelligence specific settings
    /// </summary>
    /// <param name="options">The chat options</param>
    /// <param name="temperature">Temperature for generation (0.0 to 2.0)</param>
    /// <param name="useGreedySampling">Whether to use greedy sampling for deterministic output</param>
    /// <returns>The chat options for chaining</returns>
    public static ChatOptions WithAppleIntelligenceOptions(this ChatOptions options, 
        double? temperature = null, 
        bool? useGreedySampling = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (temperature.HasValue)
        {
            options.Temperature = (float)temperature.Value;
        }

        if (useGreedySampling.HasValue)
        {
            // Store in additional properties for Apple Intelligence specific handling
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["UseGreedySampling"] = useGreedySampling.Value;
        }

        return options;
    }

    /// <summary>
    /// Gets whether greedy sampling should be used from chat options
    /// </summary>
    /// <param name="options">The chat options</param>
    /// <returns>True if greedy sampling should be used, false otherwise</returns>
    public static bool? GetUseGreedySampling(this ChatOptions? options)
    {
        return options?.AdditionalProperties?.TryGetValue("UseGreedySampling", out var value) == true 
            ? value as bool? 
            : null;
    }
}

/// <summary>
/// Error handling extensions for Apple Intelligence
/// </summary>
public static class AppleIntelligenceErrorHandling
{
    /// <summary>
    /// Safely gets a response from the chat client with automatic error handling
    /// </summary>
    /// <param name="client">The chat client</param>
    /// <param name="messages">The messages to send</param>
    /// <param name="options">Chat options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chat response or null if an error occurred</returns>
    public static async Task<ChatResponse?> SafeGetResponseAsync(
        this IChatClient client, 
        IEnumerable<ChatMessage> messages, 
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(messages);

        try
        {
            return await client.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (AppleIntelligenceException ex) when (ex.ErrorCode == AppleIntelligenceErrorCode.ContextWindowExceeded)
        {
            // Handle context window exceeded by truncating messages
            var truncatedMessages = TruncateMessages(messages);
            return await client.GetResponseAsync(truncatedMessages, options, cancellationToken);
        }
        catch (AppleIntelligenceException ex) when (ex.ErrorCode == AppleIntelligenceErrorCode.UnsupportedLanguage)
        {
            // Log the error and return null - let the caller handle language issues
            System.Diagnostics.Debug.WriteLine($"Unsupported language: {ex.Message}");
            return null;
        }
        catch (AppleIntelligenceException ex)
        {
            // Log and return null for other Apple Intelligence errors
            System.Diagnostics.Debug.WriteLine($"Apple Intelligence error: {ex.Message} (Code: {ex.ErrorCode})");
            return null;
        }
        catch (Exception ex)
        {
            // Log unexpected errors
            System.Diagnostics.Debug.WriteLine($"Unexpected error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Safely gets a streaming response with error handling
    /// </summary>
    /// <param name="client">The chat client</param>
    /// <param name="messages">The messages to send</param>
    /// <param name="options">Chat options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of chat response updates, empty if error occurred</returns>
    public static async IAsyncEnumerable<ChatResponseUpdate> SafeGetStreamingResponseAsync(
        this IChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(messages);

        List<ChatResponseUpdate> results = new();
        Exception? streamException = null;

        try
        {
            await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                results.Add(update);
            }
        }
        catch (AppleIntelligenceException ex) when (ex.ErrorCode == AppleIntelligenceErrorCode.ContextWindowExceeded)
        {
            // Try with truncated messages
            var truncatedMessages = TruncateMessages(messages);
            await foreach (var update in client.GetStreamingResponseAsync(truncatedMessages, options, cancellationToken))
            {
                results.Add(update);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Streaming error: {ex.Message}");
            streamException = ex;
        }

        // Yield the collected results
        foreach (var result in results)
        {
            yield return result;
        }
    }

    private static IEnumerable<ChatMessage> TruncateMessages(IEnumerable<ChatMessage> messages)
    {
        // Simple truncation - keep system messages and last few user/assistant messages
        var messagesList = messages.ToList();
        var systemMessages = messagesList.Where(m => m.Role == ChatRole.System);
        var otherMessages = messagesList.Where(m => m.Role != ChatRole.System).TakeLast(5);
        
        return systemMessages.Concat(otherMessages);
    }
}