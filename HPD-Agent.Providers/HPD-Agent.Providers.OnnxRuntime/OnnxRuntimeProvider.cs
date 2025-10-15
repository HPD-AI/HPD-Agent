using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntimeGenAI;
using HPD.Agent.Providers;
using HPD.Agent.ErrorHandling;
using Microsoft.Extensions.AI;

namespace HPD_Agent.Providers.OnnxRuntime;

internal class OnnxRuntimeProvider : IProviderFeatures
{
    public string ProviderKey => "onnx-runtime";
    public string DisplayName => "ONNX Runtime";

    public IChatClient CreateChatClient(ProviderConfig config, IServiceProvider? services = null)
    {
        var settings = config.ProviderSpecific?.OnnxRuntime;

        var modelPath = settings?.ModelPath ?? Environment.GetEnvironmentVariable("ONNX_MODEL_PATH");

        if (string.IsNullOrEmpty(modelPath))
        {
            throw new InvalidOperationException("For the OnnxRuntime provider, the ModelPath must be configured.");
        }

        var options = new OnnxRuntimeGenAIChatClientOptions
        {
            StopSequences = settings?.StopSequences,
            EnableCaching = settings?.EnableCaching ?? false,
            PromptFormatter = settings?.PromptFormatter
        };
        
        return new OnnxRuntimeGenAIChatClient(modelPath, options);
    }

    public IProviderErrorHandler CreateErrorHandler()
    {
        return new OnnxRuntimeErrorHandler();
    }

    public ProviderMetadata GetMetadata()
    {
        return new ProviderMetadata
        {
            ProviderKey = ProviderKey,
            DisplayName = DisplayName,
            SupportsStreaming = true,
            SupportsFunctionCalling = false,
            SupportsVision = false,
            DocumentationUrl = "https://onnxruntime.ai/docs/genai/"
        };
    }

    public ProviderValidationResult ValidateConfiguration(ProviderConfig config)
    {
        var errors = new List<string>();
        var settings = config.ProviderSpecific?.OnnxRuntime;
        var modelPath = settings?.ModelPath ?? Environment.GetEnvironmentVariable("ONNX_MODEL_PATH");

        if (string.IsNullOrEmpty(modelPath))
            errors.Add("ModelPath is required. Configure it in ProviderSpecific settings or via the ONNX_MODEL_PATH environment variable.");

        return errors.Count > 0 
            ? ProviderValidationResult.Failure(errors.ToArray())
            : ProviderValidationResult.Success();
    }
}
