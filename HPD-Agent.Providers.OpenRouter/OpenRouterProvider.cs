using System;
using System.Net.Http;
using HPD.Agent.Providers;
using HPD.Agent.ErrorHandling;
using Microsoft.Extensions.AI;

namespace HPD_Agent.Providers.OpenRouter;

internal class OpenRouterProvider : IProviderFeatures
{
    public string ProviderKey => "openrouter";
    public string DisplayName => "OpenRouter";

    public IChatClient CreateChatClient(ProviderConfig config, IServiceProvider? services = null)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
            throw new ArgumentException("OpenRouter requires an API key");

        string? httpReferer = null;
        if (config.AdditionalProperties?.TryGetValue("HttpReferer", out var refererObj) == true)
        {
            httpReferer = refererObj?.ToString();
        }

        string? appName = null;
        if (config.AdditionalProperties?.TryGetValue("AppName", out var appNameObj) == true)
        {
            appName = appNameObj?.ToString();
        }

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", httpReferer ?? "https://github.com/hpd-agent");
        httpClient.DefaultRequestHeaders.Add("X-Title", appName ?? "HPD-Agent");

        return new OpenRouterChatClient(httpClient, config.ModelName);
    }

    public IProviderErrorHandler CreateErrorHandler()
    {
        return new OpenRouterErrorHandler();
    }

    public ProviderMetadata GetMetadata()
    {
        return new ProviderMetadata
        {
            ProviderKey = ProviderKey,
            DisplayName = DisplayName,
            SupportsStreaming = true,
            SupportsFunctionCalling = true,
            SupportsVision = true, // OpenRouter supports vision models
            DocumentationUrl = "https://openrouter.ai/docs"
        };
    }

    public ProviderValidationResult ValidateConfiguration(ProviderConfig config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
            return ProviderValidationResult.Failure("API key is required for OpenRouter");

        if (string.IsNullOrEmpty(config.ModelName))
            return ProviderValidationResult.Failure("Model name is required");

        return ProviderValidationResult.Success();
    }
}
