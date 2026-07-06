# ONNX Runtime Provider

The ONNX Runtime provider lets HPD Agent use a local ONNX Runtime GenAI model directory as a chat provider.

Use it when the application owns the local model files and wants chat responses from that local model. The provider key is `onnx-runtime`.

## Package And Namespace

Reference the ONNX Runtime provider package or project, then import:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OnnxRuntime;
```

## Configure A Local Model Directory

The fluent setup requires a real directory path:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime("/absolute/path/to/onnx-genai-model")
    .BuildAsync();
```

The path must point to an ONNX Runtime GenAI model directory. A placeholder directory can prove configuration validation, but it is not enough for real inference.

If the path is null, blank, or does not exist, setup fails before the agent can run.

## Provider Family

`onnx-runtime` is a chat provider key.

It does not provide speech, image generation, embeddings, hosted files, or audio realtime clients.

The provider can opt into HPD structured tool calling for compatible local instruct models. In that mode the local model emits a constrained JSON tool-call envelope, the provider converts it to `FunctionCallContent`, and HPD executes the tool through the normal function execution path. See [ONNX Structured Tool Calling](onnx-structured-tool-calling.md).

## Configuration

The provider config type is `OnnxRuntimeProviderConfig`. The fluent helper sets `ModelPath` from the path argument. Use provider config for model loading, execution providers, and client construction:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime(
        "/absolute/path/to/onnx-genai-model",
        options =>
        {
            options.Providers = ["cuda", "cpu"];
            options.ProviderOptions = new Dictionary<string, Dictionary<string, string>>
            {
                ["cuda"] = new()
                {
                    ["device_id"] = "0"
                }
            };
        })
    .BuildAsync();
```

The provider-config path can also resolve `ONNX_MODEL_PATH` when `ModelPath` is not supplied in config. The fluent helper still requires the `modelPath` argument.

| Option | Purpose |
| --- | --- |
| `ModelPath` | ONNX Runtime GenAI model directory. |
| `Providers` | Ordered ONNX Runtime execution providers, such as `cuda`, `cpu`, `dml`, `qnn`, `openvino`, `trt`, or `webgpu`. |
| `ProviderOptions` | Provider-specific ONNX Runtime execution options passed to `Config.SetProviderOption`. |
| `HardwareDeviceType` | Decoder hardware device type applied to configured providers. |
| `HardwareDeviceId` | Decoder hardware device id applied to configured providers. |
| `HardwareVendorId` | Decoder hardware vendor id applied to configured providers. |
| `EnableCaching` | Enables the ONNX Runtime GenAI chat client's single-conversation cache. Use only for non-shared clients. |
| `EnableStructuredToolCalling` | Enables HPD's structured tool-calling adapter for compatible local instruct models. |

## Runtime Chat Options

Use HPD `ChatRunConfig` for shared runtime behavior. The ONNX Runtime GenAI MEAI chat client maps common chat options to generator search options:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime("/absolute/path/to/onnx-genai-model")
    .WithChatDefaults(new ChatRunConfig
    {
        Temperature = 0.7,
        TopP = 0.9,
        TopK = 40,
        MaxOutputTokens = 512,
        Seed = 123,
        ResponseFormat = ChatResponseFormat.Json
    })
    .BuildAsync();
```

Use `OnnxRuntimeChatRequestOptions` for ONNX Runtime GenAI search options that do not have generic chat fields:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime("/absolute/path/to/onnx-genai-model")
    .WithOnnxRuntimeChatRequestOptions(onnx =>
    {
        onnx.DoSample = true;
        onnx.NumBeams = 4;
        onnx.NumReturnSequences = 2;
        onnx.LengthPenalty = 1.2f;
    })
    .BuildAsync();
```

| Option | Search option |
| --- | --- |
| `MinLength` | `min_length` |
| `BatchSize` | `batch_size` |
| `DoSample` | `do_sample` |
| `RepetitionPenalty` | `repetition_penalty` |
| `NoRepeatNgramSize` | `no_repeat_ngram_size` |
| `NumBeams` | `num_beams` |
| `NumReturnSequences` | `num_return_sequences` |
| `EarlyStopping` | `early_stopping` |
| `LengthPenalty` | `length_penalty` |
| `DiversityPenalty` | `diversity_penalty` |
| `PastPresentShareBuffer` | `past_present_share_buffer` |
| `ChunkSize` | `chunk_size` |

The stock ONNX Runtime GenAI chat client supports JSON-schema guidance through generic `ChatRunConfig.ResponseFormat`; custom grammar or regex guidance is not exposed by HPD's provider wrapper.

## Structured Tool Calling

Use `EnableStructuredToolCalling` when the local model can follow a tool-call JSON contract:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime(
        "/absolute/path/to/onnx-genai-model",
        options =>
        {
            options.EnableStructuredToolCalling = true;
        })
    .WithChatDefaults(new ChatRunConfig
    {
        Temperature = 0,
        MaxOutputTokens = 128
    })
    .BuildAsync();
```

Tool definitions still come from HPD tools, generated harnesses, MCP, OpenAPI, or native functions. ONNX does not execute tools. HPD executes `FunctionCallContent` the same way it does for remote providers.

## Live Verification

Run the live smoke test against a real ONNX Runtime GenAI model directory:

```bash
ONNX_MODEL_PATH=/absolute/path/to/onnx-genai-model \
dotnet test test/HPD-Agent.Providers.Tests/HPD-Agent.Providers.Tests.csproj \
  --framework net10.0 \
  --filter "FullyQualifiedName~OnnxRuntimeProviderTests.LiveModel_GetResponseAsync_WithConfiguredModelPath_ReturnsText"
```

Optional variables:

| Variable | Purpose |
| --- | --- |
| `ONNX_MODEL_NAME` | Label used in provider config |
| `ONNX_SMOKE_PROMPT` | Prompt sent to the local model |
| `ONNX_SMOKE_OUTPUT_TOKENS` | Chat request output token cap |
| `ONNX_SMOKE_TIMEOUT_SECONDS` | Test cancellation timeout |

Structured tool calling has its own opt-in smoke gate:

```bash
ONNX_MODEL_PATH=/absolute/path/to/onnx-genai-model \
ONNX_TOOL_CALL_SMOKE=1 \
dotnet test test/HPD-Agent.Providers.Tests/HPD-Agent.Providers.Tests.csproj \
  --framework net10.0 \
  --filter "FullyQualifiedName~OnnxRuntimeStructuredToolCalling"
```

Optional variables:

| Variable | Purpose |
| --- | --- |
| `ONNX_TOOL_CALL_PROMPT` | Prompt used by the live tool-call smoke |
| `ONNX_TOOL_CALL_OUTPUT_TOKENS` | Chat request output token cap |
| `ONNX_TOOL_CALL_TIMEOUT_SECONDS` | Whole smoke cancellation timeout |
| `ONNX_TOOL_CALL_EVENT_TIMEOUT_SECONDS` | Agent-event wait timeout for tool execution smoke |

## Related Reading

- [Provider Setup Overview](overview.md)
- [ONNX Structured Tool Calling](onnx-structured-tool-calling.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
