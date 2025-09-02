using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

/// <summary>Represents an <see cref="IChatClient"/> for OpenRouter.</summary>
public sealed class OpenRouterChatClient : IChatClient
{
    private static readonly AIJsonSchemaTransformCache _schemaTransformCache = new(new()
    {
        ConvertBooleanSchemas = true, // Or other options as needed
    });

    private readonly ChatClientMetadata _metadata;
    private readonly Uri _apiEndpoint;
    private readonly HttpClient _httpClient;
    private readonly OpenRouterConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _openRouterOptions;

    /// <summary>Initializes a new instance of the <see cref="OpenRouterChatClient"/> class.</summary>
    /// <param name="config">The configuration for OpenRouter.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> instance to use for HTTP operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    public OpenRouterChatClient(OpenRouterConfig config, HttpClient? httpClient = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
            
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(config.ApiKey));
            
        if (string.IsNullOrWhiteSpace(config.ModelName))
            throw new ArgumentException("Model name cannot be null or empty", nameof(config.ModelName));
        
        _config = config;
        
        _apiEndpoint = string.IsNullOrWhiteSpace(config.Endpoint) 
            ? new Uri("https://openrouter.ai/api/v1/chat/completions")
            : new Uri(config.Endpoint);
            
        _httpClient = httpClient ?? new HttpClient();
        
        _metadata = new ChatClientMetadata("openrouter", _apiEndpoint, config.ModelName);
        
        _jsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                OpenRouterJsonContext.Default, 
                HPDJsonContext.Default)
        };
        
        _openRouterOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = OpenRouterJsonContext.Default
        };
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var requestPayload = CreateRequestPayload(messages, options, stream: false);
        var content = new StringContent(JsonSerializer.Serialize(requestPayload, _openRouterOptions), Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint)
        {
            Content = content
        };
        
        AddOpenRouterHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"OpenRouter API error: {response.StatusCode}, {error}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<OpenRouterResponse>(json, _openRouterOptions);
        
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize response from OpenRouter API.");
        }

        if (result.Error != null)
        {
            throw new InvalidOperationException($"OpenRouter error: {result.Error.Message}");
        }

        if (result.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("No choices returned from OpenRouter API.");
        }

        var responseId = result.Id ?? Guid.NewGuid().ToString("N");
        var chatMessage = FromOpenRouterMessage(result.Choices[0].Message, responseId);
        
        DateTimeOffset? createdAt = result.Created.HasValue 
            ? DateTimeOffset.FromUnixTimeSeconds(result.Created.Value) 
            : null;
        
        return new ChatResponse(chatMessage)
        {
            CreatedAt = createdAt,
            FinishReason = ToFinishReason(result.Choices[0].FinishReason),
            ModelId = result.Model ?? options?.ModelId ?? _metadata.DefaultModelId,
            ResponseId = responseId,
            Usage = result.Usage != null ? new UsageDetails
            {
                InputTokenCount = result.Usage.PromptTokens,
                OutputTokenCount = result.Usage.CompletionTokens,
                TotalTokenCount = result.Usage.TotalTokens
            } : null
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var requestPayload = CreateRequestPayload(messages, options, stream: true);
        var content = new StringContent(JsonSerializer.Serialize(requestPayload, _openRouterOptions), Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint) { Content = content };
        AddOpenRouterHeaders(request);

        using var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        
        if (!httpResponse.IsSuccessStatusCode)
        {
            var error = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"OpenRouter API error: {httpResponse.StatusCode}, {error}");
        }

        var responseId = Guid.NewGuid().ToString("N");
        var messageId = Guid.NewGuid().ToString("N");
        
        using var httpResponseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var streamReader = new StreamReader(httpResponseStream);
        
        string? role = null;
        bool hasYieldedAnyContent = false;
        var reasoningBuffer = new StringBuilder(); // Buffer reasoning tokens

        while (await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrEmpty(line) || line == "data: [DONE]")
                continue;
                
            if (line.StartsWith("data: "))
                line = line.Substring(6);
            
            OpenRouterResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenRouterResponse>(line, _openRouterOptions);
            }
            catch (JsonException)
            {
                continue;
            }
            
            if (chunk?.Choices == null || chunk.Choices.Count == 0)
                continue;
                
            var choice = chunk.Choices[0];
            var delta = choice.Delta;
            
            if (delta?.Role != null)
                role = delta.Role;
            
            var update = new ChatResponseUpdate
            {
                ResponseId = responseId,
                MessageId = messageId,
                Role = role != null ? new ChatRole(role) : null,
                FinishReason = ToFinishReason(choice.FinishReason),
                ModelId = chunk.Model ?? options?.ModelId ?? _metadata.DefaultModelId,
                CreatedAt = chunk.Created.HasValue ? DateTimeOffset.FromUnixTimeSeconds(chunk.Created.Value) : null
            };
            
            bool hasContentThisChunk = false;

            // Handle regular content
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                update.Contents.Add(new TextContent(delta.Content));
                hasContentThisChunk = true;
            }
            
            // Handle reasoning content - CRITICAL FIX
            if (delta?.Reasoning.HasValue == true)
            {
                string? reasoningText = null;
                
                if (delta.Reasoning.Value.ValueKind == JsonValueKind.String)
                {
                    reasoningText = delta.Reasoning.Value.GetString();
                }
                else
                {
                    reasoningText = JsonSerializer.Serialize(delta.Reasoning.Value, _jsonOptions);
                }
                
                if (!string.IsNullOrEmpty(reasoningText))
                {
                    reasoningBuffer.Append(reasoningText);
                    update.Contents.Add(new TextReasoningContent(reasoningText));
                    hasContentThisChunk = true;
                }
            }
            
            // Handle tool calls
            if (delta?.ToolCalls != null && delta.ToolCalls.Count > 0)
            {
                foreach (var toolCall in delta.ToolCalls)
                {
                    if (toolCall.Function?.Name != null)
                    {
                        var arguments = toolCall.Function.Arguments ?? "{}";
                        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, _jsonOptions);
                        update.Contents.Add(new FunctionCallContent(
                            toolCall.Id ?? Guid.NewGuid().ToString("N")[..8],
                            toolCall.Function.Name,
                            args
                        ));
                        hasContentThisChunk = true;
                    }
                }
            }
            
            // Yield update if it has content
            if (hasContentThisChunk)
            {
                hasYieldedAnyContent = true;
                yield return update;
            }
            
            // Handle completion
            if (choice.FinishReason != null)
            {
                // If we completed but never yielded content, provide fallback
                if (!hasYieldedAnyContent)
                {
                    var fallbackContent = reasoningBuffer.Length > 0 
                        ? $"[Reasoning completed: {reasoningBuffer.Length} reasoning tokens generated]\n\nThe model completed its analysis using internal reasoning but generated no visible output. For math problems like '(x-6)(x-5)(x-34) = 98', this often indicates the model spent its effort on internal calculation without showing work."
                        : "[Model completed processing but generated no visible output. This can occur with reasoning models on complex tasks.]";
                    
                    yield return new ChatResponseUpdate
                    {
                        ResponseId = responseId,
                        MessageId = messageId,
                        Role = new ChatRole(role ?? "assistant"),
                        Contents = { new TextContent(fallbackContent) },
                        ModelId = chunk.Model ?? options?.ModelId ?? _metadata.DefaultModelId,
                        CreatedAt = chunk.Created.HasValue ? DateTimeOffset.FromUnixTimeSeconds(chunk.Created.Value) : null
                    };
                    hasYieldedAnyContent = true;
                }
                
                // Yield finish reason
                yield return new ChatResponseUpdate
                {
                    ResponseId = responseId,
                    MessageId = messageId,
                    Role = new ChatRole(role ?? "assistant"),
                    FinishReason = ToFinishReason(choice.FinishReason),
                    ModelId = chunk.Model ?? options?.ModelId ?? _metadata.DefaultModelId,
                    CreatedAt = chunk.Created.HasValue ? DateTimeOffset.FromUnixTimeSeconds(chunk.Created.Value) : null
                };
                break;
            }
        }
        
        // Final safety net
        if (!hasYieldedAnyContent)
        {
            yield return new ChatResponseUpdate
            {
                ResponseId = responseId,
                MessageId = messageId,
                Role = new ChatRole("assistant"),
                Contents = { new TextContent("[No visible output generated - this can happen with reasoning models on complex tasks]") },
                ModelId = options?.ModelId ?? _metadata.DefaultModelId
            };
        }
    }

    /// <inheritdoc />
    object? IChatClient.GetService(Type serviceType, object? serviceKey)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));

        return
            serviceKey is not null ? null :
            serviceType == typeof(ChatClientMetadata) ? _metadata :
            serviceType == typeof(OpenRouterChatClient) ? this :
            null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private void AddOpenRouterHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        
        if (!string.IsNullOrEmpty(_config.HttpReferer))
        {
            request.Headers.Add("HTTP-Referer", _config.HttpReferer);
        }
        
        if (!string.IsNullOrEmpty(_config.AppName))
        {
            request.Headers.Add("X-Title", _config.AppName);
        }
    }

    private static ChatFinishReason? ToFinishReason(string? finishReason) =>
        finishReason switch
        {
            null => null,
            "length" => ChatFinishReason.Length,
            "stop" => ChatFinishReason.Stop,
            "content_filter" => ChatFinishReason.ContentFilter,
            "function_call" => ChatFinishReason.ToolCalls,
            "tool_calls" => ChatFinishReason.ToolCalls,
            _ => new ChatFinishReason(finishReason),
        };

    private static ChatMessage FromOpenRouterMessage(OpenRouterMessage? message, string responseId)
    {
        if (message == null)
        {
            return new ChatMessage(new ChatRole("assistant"), new[] { new TextContent(string.Empty) }) 
            { 
                MessageId = responseId 
            };
        }

        List<AIContent> contents = new();

        // Handle tool calls
        if (message.ToolCalls?.Count > 0)
        {
            foreach (var toolCall in message.ToolCalls)
            {
                if (toolCall.Function?.Name != null)
                {
                    var arguments = toolCall.Function.Arguments ?? "{}";
                    var args = JsonSerializer.Deserialize(arguments, OpenRouterJsonContext.Default.DictionaryStringObject);
                    contents.Add(new FunctionCallContent(
                        toolCall.Id ?? Guid.NewGuid().ToString("N")[..8],
                        toolCall.Function.Name,
                        args
                    ));
                }
            }
        }

        // Handle reasoning content - CRITICAL FIX
        if (message.Reasoning.HasValue)
        {
            string? reasoningText = null;
            
            if (message.Reasoning.Value.ValueKind == JsonValueKind.String)
            {
                reasoningText = message.Reasoning.Value.GetString();
            }
            else
            {
                reasoningText = JsonSerializer.Serialize(message.Reasoning.Value, OpenRouterJsonContext.Default.JsonElement);
            }
            
            if (!string.IsNullOrEmpty(reasoningText))
            {
                contents.Add(new TextReasoningContent(reasoningText));
            }
        }

        // Handle text content
        if (!string.IsNullOrEmpty(message.Content))
        {
            contents.Add(new TextContent(message.Content));
        }
        
        // CRITICAL: Fallback for reasoning-only responses
        if (contents.Count == 0 || (contents.Count == 1 && contents[0] is TextContent tc && string.IsNullOrEmpty(tc.Text)))
        {
            var fallbackText = message.Reasoning.HasValue 
                ? "[Response generated using internal reasoning - no visible completion content produced]"
                : "[Model completed but generated no output]";
                
            contents.Clear();
            contents.Add(new TextContent(fallbackText));
        }
        
        // If only reasoning content, add explanatory text
        if (contents.Count == 1 && contents[0] is TextReasoningContent)
        {
            contents.Insert(0, new TextContent("[Internal reasoning completed - see reasoning content below]"));
        }

        return new ChatMessage(new ChatRole(message.Role ?? "assistant"), contents) 
        { 
            MessageId = responseId 
        };
    }

    private OpenRouterRequest CreateRequestPayload(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        var request = new OpenRouterRequest
        {
            Model = options?.ModelId ?? _config.ModelName,
            Messages = messages.Select(m => ToOpenRouterMessage(m)).ToList(),
            Stream = stream,
            Temperature = options?.Temperature ?? _config.Temperature,
            MaxTokens = options?.MaxOutputTokens ?? _config.MaxTokens,
            IncludeReasoning = true // CRITICAL: Always request reasoning tokens for GPT-5 and reasoning models
        };

        if (options != null)
        {
            if (options.TopP.HasValue)
            {
                request.TopP = options.TopP.Value;
            }

            if (options.PresencePenalty.HasValue)
            {
                request.PresencePenalty = options.PresencePenalty.Value;
            }

            if (options.FrequencyPenalty.HasValue)
            {
                request.FrequencyPenalty = options.FrequencyPenalty.Value;
            }

            if (options.StopSequences != null && options.StopSequences.Count > 0)
            {
                request.Stop = options.StopSequences.ToList();
            }

            if (options.ToolMode is not NoneChatToolMode && options.Tools != null && options.Tools.Count > 0)
            {
                request.Tools = options.Tools.OfType<AIFunction>().Select(f => new OpenRouterTool
                {
                    Type = "function",
                    Function = new OpenRouterToolFunction
                    {
                        Name = f.Name,
                        Description = f.Description,
                        Parameters = JsonSerializer.Deserialize<JsonElement>(_schemaTransformCache.GetOrCreateTransformedSchema(f).GetRawText(), OpenRouterJsonContext.Default.JsonElement)
                    }
                }).ToList();
            }

            if (options.ResponseFormat is ChatResponseFormatJson)
            {
                request.ResponseFormat = new { type = "json_object" };
            }
        }

        // Check for reasoning in ExtensionOptions
        if (options?.AdditionalProperties?.TryGetValue("reasoning", out var reasoningValue) == true)
        {
            if (reasoningValue is OpenRouterReasoning reasoning)
            {
                request.Reasoning = reasoning;
            }
            else if (reasoningValue is JsonElement jsonElement)
            {
                try
                {
                    request.Reasoning = JsonSerializer.Deserialize<OpenRouterReasoning>(jsonElement.GetRawText(), _openRouterOptions);
                }
                catch (JsonException)
                {
                    // Ignore invalid reasoning configuration
                }
            }
        }

        return request;
    }

    private OpenRouterMessage ToOpenRouterMessage(ChatMessage message)
    {
        var result = new OpenRouterMessage { Role = message.Role.Value };
        
        // Extract text content
        var textContent = message.Contents.OfType<TextContent>().FirstOrDefault();
        if (textContent != null)
        {
            result.Content = textContent.Text;
        }
        else if (message.Contents.Count == 0)
        {
            result.Content = string.Empty;
        }
        
        // Handle function calls if present
        var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();
        if (functionCalls.Count > 0)
        {
            result.ToolCalls = functionCalls.Select(fc => new OpenRouterToolCall
            {
                Type = "function",
                Id = fc.CallId,
                Function = new OpenRouterFunction
                {
                    Name = fc.Name,
                    // FIX: Use AOT-safe serialization
                    Arguments = JsonSerializer.Serialize(fc.Arguments, _jsonOptions)
                }
            }).ToList();
        }
        
        // Handle function results if present
        var functionResults = message.Contents.OfType<FunctionResultContent>().FirstOrDefault();
        if (functionResults != null)
        {
            // For function results, we need to set the role to "tool"
            result.Role = "tool";
            // FIX: Use AOT-safe serialization for object
            result.Content = JsonSerializer.Serialize(functionResults.Result, _jsonOptions);
            result.ToolCallId = functionResults.CallId;
        }
        
        return result;
    }
}