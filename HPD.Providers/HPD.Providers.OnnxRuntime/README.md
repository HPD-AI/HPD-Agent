# HPD-Agent.Providers.OnnxRuntime

This package provides an integration with [ONNX Runtime](https://onnxruntime.ai/) for local model inference.

## Configuration

To use the OnnxRuntime provider, you must specify the path to your local model.

### C# Configuration

```csharp
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "onnx-runtime",
        AdditionalProperties = new()
        {
            ["ModelPath"] = "path/to/your/onnx/model/directory"
        }
    }
};
```

### JSON Configuration (`appsettings.json`)

```json
{
  "Agent": {
    "Provider": {
      "ProviderKey": "onnx-runtime",
      "AdditionalProperties": {
        "ModelPath": "path/to/your/onnx/model/directory"
      }
    }
  }
}
```

### Configuration Options

The following properties can be set via the `AdditionalProperties` dictionary:

| Key               | Type                                                  | Description                                                                                                |
|-------------------|-------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| `ModelPath`       | string                                                | **Required.** The file path to the ONNX model directory. Can also be set via the `ONNX_MODEL_PATH` environment variable. |
| `StopSequences`   | `IList<string>`                                       | Optional. A list of strings that will stop the generation of tokens.                                       |
| `EnableCaching`   | bool                                                  | Optional. Whether to enable conversation caching for better performance. Defaults to `false`.                  |
| `PromptFormatter` | `Func<IEnumerable<ChatMessage>, ChatOptions?, string>` | Optional. A custom function for advanced prompt engineering to format the prompt sent to the model.        |
