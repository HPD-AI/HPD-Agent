# HPD-Agent.Providers.AzureAIInference

This package provides an integration with [Azure AI Inference](https://learn.microsoft.com/en-us/azure/ai-studio/how-to/deploy-models-inference), allowing you to use models deployed in your Azure AI environment.

## Configuration

To use the Azure AI Inference provider, you must provide an endpoint and an API key.

### C# Configuration

```csharp
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "azure-ai-inference",
        ModelName = "YOUR_DEPLOYMENT_NAME", // e.g., "llama-3-8b"
        Endpoint = "https://<your-resource-name>.inference.ai.azure.com", // Can also be set in AdditionalProperties
        ApiKey = "YOUR_AZURE_AI_API_KEY" // Can also be set in AdditionalProperties
    }
};

// Alternatively, using AdditionalProperties:
var configWithAdditionalProps = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "azure-ai-inference",
        ModelName = "YOUR_DEPLOYMENT_NAME",
        AdditionalProperties = new()
        {
            ["Endpoint"] = "https://<your-resource-name>.inference.ai.azure.com",
            ["ApiKey"] = "YOUR_AZURE_AI_API_KEY"
        }
    }
};
```

### JSON Configuration (`appsettings.json`)

```json
{
  "Agent": {
    "Provider": {
      "ProviderKey": "azure-ai-inference",
      "ModelName": "YOUR_DEPLOYMENT_NAME",
      "Endpoint": "https://<your-resource-name>.inference.ai.azure.com",
      "ApiKey": "YOUR_AZURE_AI_API_KEY"
    }
  }
}
```
*Note: You can also place `Endpoint` and `ApiKey` inside an `AdditionalProperties` object in the JSON file.*

### Configuration Options

The following properties can be set. The primary properties on `ProviderConfig` (`Endpoint`, `ApiKey`) are checked first, followed by `AdditionalProperties`, and finally environment variables.

| Key         | Type   | Description                                                                                                |
|-------------|--------|------------------------------------------------------------------------------------------------------------|
| `Endpoint`  | string | **Required.** The unified endpoint for the Azure AI resource. Can also be set via the `AZURE_AI_INFERENCE_ENDPOINT` environment variable. |
| `ApiKey`    | string | **Required.** The API key for the Azure AI resource. Can also be set via the `AZURE_AI_INFERENCE_API_KEY` environment variable. |
