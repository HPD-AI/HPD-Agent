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

The provider config type is `OnnxRuntimeProviderConfig`. The fluent helper sets `ModelPath` from the path argument:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime(
        "/absolute/path/to/onnx-genai-model",
        options =>
        {
            options.MaxLength = 512;
            options.Temperature = 0.7f;
            options.TopP = 0.9f;
        })
    .BuildAsync();
```

The provider-config path can also resolve `ONNX_MODEL_PATH` when `ModelPath` is not supplied in config. The fluent helper still requires the `modelPath` argument.

## Structured Tool Calling

Use `EnableStructuredToolCalling` when the local model can follow a tool-call JSON contract:

```csharp
var agent = await new AgentBuilder()
    .WithOnnxRuntime(
        "/absolute/path/to/onnx-genai-model",
        options =>
        {
            options.EnableStructuredToolCalling = true;
            options.Temperature = 0;
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
| `ONNX_SMOKE_MAX_LENGTH` | Provider config max length |
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
| `ONNX_TOOL_CALL_MAX_LENGTH` | Provider config max length for the smoke |
| `ONNX_TOOL_CALL_OUTPUT_TOKENS` | Chat request output token cap |
| `ONNX_TOOL_CALL_TIMEOUT_SECONDS` | Whole smoke cancellation timeout |
| `ONNX_TOOL_CALL_EVENT_TIMEOUT_SECONDS` | Agent-event wait timeout for tool execution smoke |

## Related Reading

- [Provider Setup Overview](overview.md)
- [ONNX Structured Tool Calling](onnx-structured-tool-calling.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
