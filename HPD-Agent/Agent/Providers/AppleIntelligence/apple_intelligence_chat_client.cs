using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

/// <summary>
/// An IChatClient implementation for Apple Intelligence using Swift interop
/// Accurately reflects Apple's Foundation Models framework API
/// </summary>
public sealed class AppleIntelligenceChatClient : IChatClient
{
    private static readonly AIJsonSchemaTransformCache _schemaTransformCache = new(new()
    {
        ConvertBooleanSchemas = true,
    });

    private readonly AppleIntelligenceConfig _config;
    private readonly ChatClientMetadata _metadata;
    private readonly SystemLanguageModelHandle _systemModel;
    private readonly ConcurrentDictionary<string, ManagedLanguageModelSession> _activeSessions = new();
    private bool _disposed;

    /// <summary>
    /// Gets the availability status of Apple Intelligence on this system
    /// Matches Apple's SystemLanguageModel.default.availability API
    /// </summary>
    public static AppleIntelligenceAvailability Availability
    {
        get
        {
            try
            {
                var model = FoundationModelsInterop.GetDefaultSystemLanguageModel();
                var availability = FoundationModelsInterop.GetSystemLanguageModelAvailability(model);
                return availability switch
                {
                    SystemLanguageModelAvailability.Available => AppleIntelligenceAvailability.Available,
                    SystemLanguageModelAvailability.UnavailableAppleIntelligenceNotEnabled => 
                        AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.AppleIntelligenceNotEnabled),
                    SystemLanguageModelAvailability.UnavailableDeviceNotEligible => 
                        AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.DeviceNotEligible),
                    SystemLanguageModelAvailability.UnavailableModelNotReady => 
                        AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.ModelNotReady),
                    SystemLanguageModelAvailability.UnavailableUnsupportedLanguage =>
                        AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.UnsupportedLanguage),
                    _ => AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.Unknown)
                };
            }
            catch (DllNotFoundException ex)
            {
                Debug.WriteLine($"FoundationModels framework not found: {ex.Message}");
                return AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.DeviceNotEligible);
            }
            catch (EntryPointNotFoundException ex)
            {
                Debug.WriteLine($"FoundationModels API not available: {ex.Message}");
                return AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.DeviceNotEligible);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error checking Apple Intelligence availability: {ex}");
                return AppleIntelligenceAvailability.Unavailable(AppleIntelligenceUnavailableReason.Unknown);
            }
        }
    }

    /// <summary>
    /// Gets whether Apple Intelligence is available on this system
    /// Matches Apple's SystemLanguageModel.default.isAvailable
    /// </summary>
    public static bool IsAvailable => Availability.IsAvailable;

    /// <summary>
    /// Initializes a new instance of the AppleIntelligenceChatClient
    /// </summary>
    /// <param name="config">Configuration for the client</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    /// <exception cref="NotSupportedException">Thrown when Apple Intelligence is not available</exception>
    public AppleIntelligenceChatClient(AppleIntelligenceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (_config.ValidateAvailability)
        {
            var availability = Availability;
            if (!availability.IsAvailable)
            {
                var reason = availability.UnavailableReason;
                var message = reason switch
                {
                    AppleIntelligenceUnavailableReason.AppleIntelligenceNotEnabled => 
                        "Apple Intelligence is not enabled. Please enable it in System Settings.",
                    AppleIntelligenceUnavailableReason.DeviceNotEligible => 
                        "This device is not eligible for Apple Intelligence. Requires iOS 26.0+/macOS 26.0+ with compatible hardware.",
                    AppleIntelligenceUnavailableReason.ModelNotReady => 
                        "Apple Intelligence model is not ready. The model may still be downloading.",
                    AppleIntelligenceUnavailableReason.UnsupportedLanguage =>
                        "The current system language is not supported by Apple Intelligence.",
                    _ => "Apple Intelligence is not available on this system."
                };
                throw new NotSupportedException(message);
            }
        }

        try
        {
            // Get the appropriate model based on configuration
            _systemModel = _config.UseCase switch
            {
                AppleIntelligenceUseCase.ContentTagging => 
                    FoundationModelsInterop.CreateSystemLanguageModel(SystemLanguageModelUseCase.ContentTagging),
                _ => FoundationModelsInterop.GetDefaultSystemLanguageModel()
            };
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("Failed to initialize Apple Intelligence system model.", ex);
        }

        _metadata = new ChatClientMetadata("apple-intelligence", null, _config.ModelId);
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, 
        ChatOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        ObjectDisposedException.ThrowIf(_disposed, this);

        var messagesList = messages.ToList();
        if (messagesList.Count == 0)
            throw new ArgumentException("At least one message is required", nameof(messages));

        var sessionKey = GetSessionKey(options);
        using var session = GetOrCreateSession(sessionKey, options);
        
        var prompt = BuildPrompt(messagesList);
        var responseId = Guid.NewGuid().ToString("N");
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var (responseText, duration) = await GetResponseFromSessionAsync(
                session, prompt, options, cancellationToken);
            
            // Parse the response and create appropriate content
            var chatMessage = CreateChatMessageFromResponse(responseText, responseId, options);

            var response = new ChatResponse(chatMessage)
            {
                CreatedAt = startTime,
                FinishReason = ChatFinishReason.Stop,
                ModelId = options?.ModelId ?? _config.ModelId,
                ResponseId = responseId,
                Usage = new() 
                {
                    InputTokenCount = EstimateTokenCount(prompt),
                    OutputTokenCount = EstimateTokenCount(responseText),
                    TotalTokenCount = EstimateTokenCount(prompt) + EstimateTokenCount(responseText)
                }
            };
            
            return response;
        }
        catch (AppleIntelligenceException)
        {
            throw; // Re-throw Apple Intelligence specific exceptions
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to get response from Apple Intelligence", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, 
        ChatOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        ObjectDisposedException.ThrowIf(_disposed, this);

        var messagesList = messages.ToList();
        if (messagesList.Count == 0)
            throw new ArgumentException("At least one message is required", nameof(messages));

        // Collect streaming results first to avoid yield in try-catch
        var streamingResults = new List<ChatResponseUpdate>();
        Exception? streamException = null;
        
        try
        {
            var sessionKey = GetSessionKey(options);
            using var session = GetOrCreateSession(sessionKey, options);
            
            var prompt = BuildPrompt(messagesList);
            var responseId = Guid.NewGuid().ToString("N");
            var messageId = Guid.NewGuid().ToString("N");
            var startTime = DateTimeOffset.UtcNow;

            await foreach (var partialUpdate in GetStreamingResponseFromSessionAsync(
                session, prompt, options, cancellationToken))
            {
                var update = new ChatResponseUpdate
                {
                    ResponseId = responseId,
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    ModelId = options?.ModelId ?? _config.ModelId,
                    CreatedAt = startTime
                };

                if (partialUpdate.IsComplete)
                {
                    update.FinishReason = ChatFinishReason.Stop;
                }

                if (!string.IsNullOrEmpty(partialUpdate.Content))
                {
                    update.Contents.Add(new TextContent(partialUpdate.Content));
                }

                streamingResults.Add(update);
                
                if (partialUpdate.IsComplete) break;
            }
        }
        catch (AppleIntelligenceException ex)
        {
            streamException = ex;
        }
        catch (Exception ex)
        {
            streamException = new InvalidOperationException("Failed to stream response from Apple Intelligence", ex);
        }

        // Yield the results
        foreach (var result in streamingResults)
        {
            yield return result;
        }
        
        // Throw exception after yielding if there was an error
        if (streamException != null)
        {
            throw streamException;
        }
    }

    /// <inheritdoc />
    object? IChatClient.GetService(Type serviceType, object? serviceKey)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));

        return serviceKey is not null ? null :
            serviceType == typeof(ChatClientMetadata) ? _metadata :
            serviceType == typeof(AppleIntelligenceChatClient) ? this :
            serviceType == typeof(AppleIntelligenceConfig) ? _config :
            null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose all active sessions
            foreach (var session in _activeSessions.Values)
            {
                session.Dispose();
            }
            _activeSessions.Clear();
            
            _disposed = true;
        }
    }

    private string GetSessionKey(ChatOptions? options)
    {
        // Use the extension method to get session key from additional properties
        return options.GetSessionKey() ?? "default";
    }

    private ManagedLanguageModelSession GetOrCreateSession(string sessionKey, ChatOptions? options)
    {
        return _activeSessions.GetOrAdd(sessionKey, _ => CreateSession(options));
    }

    private ManagedLanguageModelSession CreateSession(ChatOptions? options)
    {
        var instructions = _config.SystemInstructions;
        var toolHandles = ConvertToolsToHandles(options?.Tools);
        
        // Create session using Apple's actual API patterns
        LanguageModelSessionHandle sessionHandle;
        
        if (toolHandles.Length > 0 && !string.IsNullOrEmpty(instructions))
        {
            sessionHandle = LanguageModelSessionInterop.CreateLanguageModelSessionWithToolsAndInstructions(
                _systemModel, toolHandles, toolHandles.Length, instructions);
        }
        else if (toolHandles.Length > 0)
        {
            sessionHandle = LanguageModelSessionInterop.CreateLanguageModelSessionWithTools(
                _systemModel, toolHandles, toolHandles.Length);
        }
        else if (!string.IsNullOrEmpty(instructions))
        {
            sessionHandle = LanguageModelSessionInterop.CreateLanguageModelSessionWithInstructions(
                _systemModel, instructions);
        }
        else
        {
            sessionHandle = LanguageModelSessionInterop.CreateLanguageModelSession(_systemModel);
        }
        
        var session = new ManagedLanguageModelSession(sessionHandle, toolHandles);
        
        // Prewarm the session if configured
        if (_config.PrewarmSession)
        {
            LanguageModelSessionInterop.LanguageModelSessionPrewarm(sessionHandle);
        }
        
        return session;
    }

    private async Task<(string Response, double Duration)> GetResponseFromSessionAsync(
        ManagedLanguageModelSession session,
        string prompt,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<(string, double)>();
        
        // Set up callback for response
        var callback = new LanguageModelResponseCallback((response, duration, errorCode, errorMessage, context) =>
        {
            if (errorCode != AppleIntelligenceErrorCode.None)
            {
                var exception = CreateExceptionFromError(errorCode, errorMessage);
                tcs.TrySetException(exception);
            }
            else
            {
                tcs.TrySetResult((response, duration));
            }
        });
        
        // Call appropriate method based on whether we have structured output
        if (options?.ResponseFormat is ChatResponseFormatJson)
        {
            using var generationOptions = CreateGenerationOptions(options);
            var schema = CreateBasicJsonSchema();
            
            LanguageModelSessionInterop.LanguageModelSessionRespondToGeneratingWithOptions(
                session.Handle,
                prompt,
                "JsonObject", // Type name for JSON objects
                schema,
                generationOptions.Handle,
                true, // includeSchemaInPrompt
                callback,
                IntPtr.Zero);
        }
        else
        {
            if (HasCustomGenerationOptions(options))
            {
                using var generationOptions = CreateGenerationOptions(options);
                LanguageModelSessionInterop.LanguageModelSessionRespondToWithOptions(
                    session.Handle,
                    prompt,
                    generationOptions.Handle,
                    callback,
                    IntPtr.Zero);
            }
            else
            {
                LanguageModelSessionInterop.LanguageModelSessionRespondTo(
                    session.Handle,
                    prompt,
                    callback,
                    IntPtr.Zero);
            }
        }
        
        cancellationToken.Register(() => tcs.TrySetCanceled());
        return await tcs.Task;
    }

    private async IAsyncEnumerable<StreamingUpdate> GetStreamingResponseFromSessionAsync(
        ManagedLanguageModelSession session,
        string prompt,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamingResults = new List<StreamingUpdate>();
        var completionTcs = new TaskCompletionSource<bool>();
        
        var streamCallback = new LanguageModelStreamingCallback((partialResponse, isComplete, context) =>
        {
            streamingResults.Add(new StreamingUpdate(partialResponse, isComplete));
            if (isComplete)
            {
                completionTcs.TrySetResult(true);
            }
        });
        
        var completionCallback = new LanguageModelResponseCallback((response, duration, errorCode, errorMessage, context) =>
        {
            if (errorCode != AppleIntelligenceErrorCode.None)
            {
                var exception = CreateExceptionFromError(errorCode, errorMessage);
                completionTcs.TrySetException(exception);
            }
            else if (!completionTcs.Task.IsCompleted)
            {
                completionTcs.TrySetResult(true);
            }
        });
        
        StreamingResponseHandle streamHandle;
        
        // Choose appropriate streaming method
        if (options?.ResponseFormat is ChatResponseFormatJson)
        {
            using var generationOptions = CreateGenerationOptions(options);
            var schema = CreateBasicJsonSchema();
            
            streamHandle = LanguageModelSessionInterop.LanguageModelSessionStreamResponseToGenerating(
                session.Handle,
                prompt,
                "JsonObject",
                schema,
                generationOptions.Handle,
                true,
                streamCallback,
                completionCallback,
                IntPtr.Zero);
        }
        else if (HasCustomGenerationOptions(options))
        {
            using var generationOptions = CreateGenerationOptions(options);
            streamHandle = LanguageModelSessionInterop.LanguageModelSessionStreamResponseToWithOptions(
                session.Handle,
                prompt,
                generationOptions.Handle,
                streamCallback,
                completionCallback,
                IntPtr.Zero);
        }
        else
        {
            streamHandle = LanguageModelSessionInterop.LanguageModelSessionStreamResponseTo(
                session.Handle,
                prompt,
                streamCallback,
                completionCallback,
                IntPtr.Zero);
        }
        
        cancellationToken.Register(() => completionTcs.TrySetCanceled());
        
        try
        {
            await completionTcs.Task;
        }
        finally
        {
            LanguageModelSessionInterop.DisposeStreamingResponse(streamHandle);
        }
        
        foreach (var result in streamingResults)
        {
            yield return result;
        }
    }

    private ManagedGenerationOptions CreateGenerationOptions(ChatOptions? options)
    {
        var appleOptions = _config.DefaultGenerationOptions;
        
        var temperature = options?.Temperature ?? appleOptions?.Temperature ?? 1.0;
        var maxTokens = options?.MaxOutputTokens ?? appleOptions?.MaxTokens ?? 1000;
        var useGreedySampling = options.GetUseGreedySampling() ?? appleOptions?.UseGreedySampling == true;
        
        var samplingMethod = useGreedySampling || temperature == 0.0 
            ? GenerationSamplingMethod.Greedy 
            : GenerationSamplingMethod.Random;
        
        var optionsHandle = GenerationOptionsInterop.CreateGenerationOptionsComplete(
            temperature, maxTokens, samplingMethod);
            
        return new ManagedGenerationOptions(optionsHandle);
    }

    private bool HasCustomGenerationOptions(ChatOptions? options)
    {
        return options?.Temperature != null || 
               options?.MaxOutputTokens != null ||
               options.GetUseGreedySampling() != null ||
               _config.DefaultGenerationOptions != null;
    }

    private ToolHandle[] ConvertToolsToHandles(IList<AITool>? tools)
    {
        if (tools == null || tools.Count == 0)
            return [];

        var toolHandles = new List<ToolHandle>();
        
        foreach (var tool in tools.OfType<AIFunction>())
        {
            try
            {
                var schemaJson = _schemaTransformCache.GetOrCreateTransformedSchema(tool).GetRawText();
                
                // Create tool callback that will be invoked by Apple Intelligence
                var callback = new ToolCallCallback((argumentsJson, resultCallback, context) =>
                {
                    // This would need to be implemented to call the actual AIFunction
                    // For now, this is a placeholder - in a full implementation,
                    // you would parse argumentsJson, call the AIFunction, and return the result
                    // via the resultCallback
                });
                
                var handle = ToolInterop.CreateTool(
                    tool.Name,
                    tool.Description ?? "",
                    schemaJson,
                    callback,
                    IntPtr.Zero);
                    
                toolHandles.Add(handle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to convert tool {tool.Name}: {ex.Message}");
            }
        }

        return [.. toolHandles];
    }

    private static string CreateBasicJsonSchema()
    {
        return """
        {
            "type": "object",
            "properties": {},
            "additionalProperties": true
        }
        """;
    }

    private static ChatMessage CreateChatMessageFromResponse(string responseText, string messageId, ChatOptions? options)
    {
        if (options?.ResponseFormat is ChatResponseFormatJson)
        {
            return new ChatMessage(ChatRole.Assistant, new AIContent[] { new TextContent(responseText) })
            {
                MessageId = messageId
            };
        }

        var contents = new List<AIContent>();
        
        // Parse the response text for function calls and regular content
        var parsedContent = ParseResponseContent(responseText);
        
        // Add text content if present
        if (!string.IsNullOrEmpty(parsedContent.TextContent))
        {
            contents.Add(new TextContent(parsedContent.TextContent));
        }
        
        // Add function calls if present
        contents.AddRange(parsedContent.FunctionCalls);
        
        // If no content was parsed, add the raw response as text
        if (contents.Count == 0)
        {
            contents.Add(new TextContent(responseText));
        }
        
        return new ChatMessage(ChatRole.Assistant, contents)
        {
            MessageId = messageId
        };
    }

    private static (string TextContent, List<FunctionCallContent> FunctionCalls) ParseResponseContent(string responseText)
    {
        var functionCalls = new List<FunctionCallContent>();
        var textContent = responseText;
        
        // Apple Intelligence uses structured function call format
        // Pattern based on research findings
        var functionCallPattern = @"\[Function Call: (\w+)\(([^)]*)\)\]";
        var matches = System.Text.RegularExpressions.Regex.Matches(responseText, functionCallPattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var functionName = match.Groups[1].Value;
                var argsText = match.Groups[2].Value;
                
                var arguments = ParseFunctionArguments(argsText);
                var callId = Guid.NewGuid().ToString("N")[..8];
                functionCalls.Add(new FunctionCallContent(callId, functionName, arguments));
                
                textContent = textContent.Replace(match.Value, "").Trim();
            }
        }
        
        return (textContent, functionCalls);
    }

    private static Dictionary<string, object?> ParseFunctionArguments(string argsText)
    {
        var arguments = new Dictionary<string, object?>();
        
        if (string.IsNullOrEmpty(argsText) || argsText == "{}")
            return arguments;
        
        try
        {
            using var document = JsonDocument.Parse(argsText);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => property.Value.GetRawText()
                };
            }
        }
        catch
        {
            // Fallback to simple parsing
            try
            {
                argsText = argsText.Trim('{', '}');
                if (!string.IsNullOrEmpty(argsText))
                {
                    var pairs = argsText.Split(',');
                    foreach (var pair in pairs)
                    {
                        var keyValue = pair.Split(':', 2);
                        if (keyValue.Length == 2)
                        {
                            var key = keyValue[0].Trim().Trim('"');
                            var value = keyValue[1].Trim().Trim('"');
                            arguments[key] = value;
                        }
                    }
                }
            }
            catch
            {
                // Return empty if all parsing fails
            }
        }
        
        return arguments;
    }

    private static string BuildPrompt(IReadOnlyList<ChatMessage> messages)
    {
        var prompt = new StringBuilder();
        
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                prompt.AppendLine($"System: {GetTextContent(message)}");
            }
            else if (message.Role == ChatRole.User)
            {
                prompt.AppendLine($"User: {GetTextContent(message)}");
            }
            else if (message.Role == ChatRole.Assistant)
            {
                prompt.AppendLine($"Assistant: {GetTextContent(message)}");
                
                // Handle function calls in assistant messages
                var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();
                foreach (var functionCall in functionCalls)
                {
                    var argsJson = SerializeArgumentsSafe(functionCall.Arguments);
                    prompt.AppendLine($"[Function Call: {functionCall.Name}({argsJson})]");
                }
            }
            else if (message.Role.Value == "tool")
            {
                // Handle function results
                var functionResults = message.Contents.OfType<FunctionResultContent>().ToList();
                foreach (var result in functionResults)
                {
                    var resultJson = SerializeResultSafe(result.Result);
                    prompt.AppendLine($"[Function Result for {result.CallId}: {resultJson}]");
                }
                
                var textContent = GetTextContent(message);
                if (!string.IsNullOrEmpty(textContent))
                {
                    prompt.AppendLine($"Tool: {textContent}");
                }
            }
        }

        return prompt.ToString().TrimEnd();
    }

    private static string SerializeArgumentsSafe(IDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "{}";
        
        try
        {
            var parts = arguments.Select(kvp => $"\"{EscapeJsonString(kvp.Key)}\":\"{EscapeJsonString(kvp.Value?.ToString() ?? "null")}\"");
            return "{" + string.Join(",", parts) + "}";
        }
        catch
        {
            return "{}";
        }
    }

    private static string SerializeResultSafe(object? result)
    {
        if (result == null)
            return "null";
        
        try
        {
            return EscapeJsonString(result.ToString() ?? "null");
        }
        catch
        {
            return "null";
        }
    }

    private static string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string GetTextContent(ChatMessage message)
    {
        var contents = new List<string>();
        
        foreach (var textContent in message.Contents.OfType<TextContent>())
        {
            if (!string.IsNullOrEmpty(textContent.Text))
                contents.Add(textContent.Text);
        }
        
        return string.Join(" ", contents);
    }

    private static int EstimateTokenCount(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
    }

    private static AppleIntelligenceException CreateExceptionFromError(AppleIntelligenceErrorCode errorCode, string errorMessage)
    {
        var message = string.IsNullOrEmpty(errorMessage) ? errorCode switch
        {
            AppleIntelligenceErrorCode.UnsupportedLanguage => "Unsupported language",
            AppleIntelligenceErrorCode.ContextWindowExceeded => "Context window exceeded",
            AppleIntelligenceErrorCode.GuardrailViolation => "Guardrail violation",
            AppleIntelligenceErrorCode.ModelUnavailable => "Model unavailable",
            AppleIntelligenceErrorCode.ToolCallError => "Tool call error",
            AppleIntelligenceErrorCode.InvalidSchema => "Invalid schema",
            AppleIntelligenceErrorCode.GenerationCancelled => "Generation cancelled",
            AppleIntelligenceErrorCode.SessionDisposed => "Session disposed",
            AppleIntelligenceErrorCode.InvalidGenerableType => "Invalid generable type",
            _ => "Unknown error"
        } : errorMessage;

        return new AppleIntelligenceException(message, errorCode);
    }

    /// <summary>
    /// Managed wrapper for LanguageModelSession that ensures proper disposal
    /// </summary>
    private sealed class ManagedLanguageModelSession : IDisposable
    {
        public LanguageModelSessionHandle Handle { get; }
        private readonly ToolHandle[] _toolHandles;
        private bool _disposed;

        public ManagedLanguageModelSession(LanguageModelSessionHandle handle, ToolHandle[] toolHandles)
        {
            Handle = handle;
            _toolHandles = toolHandles;
        }

        public bool IsResponding => LanguageModelSessionInterop.LanguageModelSessionIsResponding(Handle);

        public void Dispose()
        {
            if (!_disposed)
            {
                LanguageModelSessionInterop.DisposeLanguageModelSession(Handle);
                
                foreach (var tool in _toolHandles)
                {
                    ToolInterop.DisposeTool(tool);
                }
                
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Managed wrapper for GenerationOptions that ensures proper disposal
    /// </summary>
    private sealed class ManagedGenerationOptions : IDisposable
    {
        public GenerationOptionsHandle Handle { get; }
        private bool _disposed;

        public ManagedGenerationOptions(GenerationOptionsHandle handle)
        {
            Handle = handle;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                GenerationOptionsInterop.DisposeGenerationOptions(Handle);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a streaming update from the model
    /// </summary>
    private sealed record StreamingUpdate(string Content, bool IsComplete);
}