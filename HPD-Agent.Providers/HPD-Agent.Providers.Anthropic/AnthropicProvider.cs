using System;
using System.Collections.Generic;
using Anthropic.SDK;
using HPD.Agent.Providers;
using HPD.Agent.ErrorHandling;
using Microsoft.Extensions.AI;

namespace HPD_Agent.Providers.Anthropic;

internal class AnthropicProvider : IProviderFeatures
{
    public string ProviderKey => "anthropic";
    public string DisplayName => "Anthropic (Claude)";

    public IChatClient CreateChatClient(ProviderConfig config, IServiceProvider? services = null)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
            throw new ArgumentException("Anthropic requires an API key");

        var anthropicClient = new AnthropicClient(config.ApiKey);
        return anthropicClient.Messages;
    }

    public IProviderErrorHandler CreateErrorHandler()
    {
        return new AnthropicErrorHandler();
    }

    public ProviderMetadata GetMetadata()
    {
        return new ProviderMetadata
        {
            ProviderKey = ProviderKey,
            DisplayName = DisplayName,
            SupportsStreaming = true,
            SupportsFunctionCalling = true,
            SupportsVision = true,
            DefaultContextWindow = 200000, // Claude 3.5 Sonnet
            DocumentationUrl = "https://docs.anthropic.com/"
        };
    }

    public ProviderValidationResult ValidateConfiguration(ProviderConfig config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
            return ProviderValidationResult.Failure("API key is required for Anthropic");

        if (string.IsNullOrEmpty(config.ModelName))
            return ProviderValidationResult.Failure("Model name is required");

        return ProviderValidationResult.Success();
    }
}
