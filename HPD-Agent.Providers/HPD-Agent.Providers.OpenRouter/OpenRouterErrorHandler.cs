using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using HPD.Agent.ErrorHandling;

namespace HPD_Agent.Providers.OpenRouter;

internal class OpenRouterErrorHandler : IProviderErrorHandler
{
    public ProviderErrorDetails? ParseError(Exception exception)
    {
        if (exception is HttpRequestException httpEx)
        {
            var statusCode = (int?)httpEx.StatusCode;
            var message = httpEx.Message;
            string? errorCode = null;
            string? requestId = null;
            TimeSpan? retryAfter = null;
            var rawDetails = new Dictionary<string, object>();

            // Try to parse JSON error body from message
            // OpenRouter format: {"error": {"code": 402, "message": "...", "metadata": {...}}}
            var jsonMatch = Regex.Match(message, @"\{.*""error"".*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                try
                {
                    using var doc = JsonDocument.Parse(jsonMatch.Value);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        // Extract error message
                        if (errorElement.TryGetProperty("message", out var msgElement))
                        {
                            message = msgElement.GetString() ?? message;
                        }

                        // Extract error code (string like "insufficient_credits")
                        if (errorElement.TryGetProperty("code", out var codeElement))
                        {
                            errorCode = codeElement.ValueKind == JsonValueKind.String
                                ? codeElement.GetString()
                                : codeElement.GetInt32().ToString();
                        }

                        // Extract metadata (provider info, moderation flags, etc.)
                        if (errorElement.TryGetProperty("metadata", out var metadataElement))
                        {
                            if (metadataElement.TryGetProperty("provider_name", out var providerElement))
                            {
                                rawDetails["provider_name"] = providerElement.GetString() ?? "";
                            }
                            if (metadataElement.TryGetProperty("flagged_input", out var flaggedElement))
                            {
                                rawDetails["flagged_input"] = flaggedElement.GetString() ?? "";
                            }
                            if (metadataElement.TryGetProperty("reasons", out var reasonsElement))
                            {
                                rawDetails["moderation_reasons"] = reasonsElement.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    // JSON parsing failed, continue with original message
                }
            }

            // Extract request ID from message (common patterns)
            var requestIdMatch = Regex.Match(message, @"request[_-]?id[:\s]+([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            if (requestIdMatch.Success)
            {
                requestId = requestIdMatch.Groups[1].Value;
            }

            // Extract Retry-After from message (e.g., "retry after 5s" or "try again in 2.5s")
            var retryMatch = Regex.Match(message, @"(?:retry after|try again in)\s+(\d+(?:\.\d+)?)\s*s", RegexOptions.IgnoreCase);
            if (retryMatch.Success && double.TryParse(retryMatch.Groups[1].Value, out var seconds))
            {
                retryAfter = TimeSpan.FromSeconds(seconds);
            }

            // Extract X-RateLimit-Reset from message (timestamp in seconds)
            var rateLimitResetMatch = Regex.Match(message, @"X-RateLimit-Reset[:\s]+(\d+)", RegexOptions.IgnoreCase);
            if (rateLimitResetMatch.Success && long.TryParse(rateLimitResetMatch.Groups[1].Value, out var resetTimestamp))
            {
                var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp);
                var delayUntilReset = resetTime - DateTimeOffset.UtcNow;
                if (delayUntilReset > TimeSpan.Zero)
                {
                    retryAfter = delayUntilReset;
                }
            }

            var category = ClassifyError(statusCode, message, errorCode);

            return new ProviderErrorDetails
            {
                StatusCode = statusCode,
                Category = category,
                Message = message,
                ErrorCode = errorCode,
                RequestId = requestId,
                RetryAfter = retryAfter,
                RawDetails = rawDetails.Count > 0 ? rawDetails : null
            };
        }
        return null;
    }

    public TimeSpan? GetRetryDelay(ProviderErrorDetails details, int attempt, TimeSpan initialDelay, double multiplier, TimeSpan maxDelay)
    {
        // Priority 1: Use RetryAfter from provider if available
        if (details.RetryAfter.HasValue && details.Category == ErrorCategory.RateLimitRetryable)
        {
            return details.RetryAfter.Value;
        }

        // Priority 2: Exponential backoff for retryable errors
        if (details.Category is ErrorCategory.RateLimitRetryable or ErrorCategory.ServerError or ErrorCategory.Transient)
        {
            var baseMs = initialDelay.TotalMilliseconds;
            var expDelayMs = baseMs * Math.Pow(multiplier, attempt);
            return TimeSpan.FromMilliseconds(Math.Min(expDelayMs, maxDelay.TotalMilliseconds));
        }

        // Don't retry terminal errors
        return null;
    }

    public bool RequiresSpecialHandling(ProviderErrorDetails details)
    {
        // Auth errors may need token refresh
        return details.Category == ErrorCategory.AuthError;
    }

    private static ErrorCategory ClassifyError(int? status, string message, string? errorCode)
    {
        // Check error code first (more specific than status)
        if (!string.IsNullOrEmpty(errorCode))
        {
            if (errorCode.Contains("insufficient_credit", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("no_credit", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorCategory.RateLimitTerminal; // Don't retry - need to add credits
            }

            if (errorCode.Contains("rate_limit", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorCategory.RateLimitRetryable;
            }

            if (errorCode.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorCategory.Transient;
            }

            if (errorCode.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("content_filter", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorCategory.ClientError; // Don't retry - content was flagged
            }
        }

        // Classify by HTTP status code
        return status switch
        {
            400 => ErrorCategory.ClientError,           // Bad request
            401 => ErrorCategory.AuthError,             // Invalid credentials
            402 => ErrorCategory.RateLimitTerminal,     // Insufficient credits (don't retry)
            403 => message.Contains("moderation", StringComparison.OrdinalIgnoreCase)
                ? ErrorCategory.ClientError              // Content flagged (don't retry)
                : ErrorCategory.AuthError,               // Other auth issue
            408 => ErrorCategory.Transient,             // Request timeout (retryable)
            429 => ErrorCategory.RateLimitRetryable,    // Rate limit (retryable)
            502 => ErrorCategory.ServerError,           // Model unavailable (retryable)
            503 => ErrorCategory.ServerError,           // No provider available (retryable)
            >= 500 and < 600 => ErrorCategory.ServerError, // Other server errors (retryable)
            _ => ErrorCategory.Unknown
        };
    }
}
