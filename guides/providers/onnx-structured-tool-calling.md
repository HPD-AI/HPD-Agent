# ONNX Structured Tool Calling

ONNX Runtime structured tool calling lets a local ONNX Runtime GenAI model request HPD tools through the same `FunctionCallContent` path used by other chat providers.

This is HPD-owned structured tool calling over local generation. It is not a remote provider's native function-calling API, and ONNX does not execute tools. The local model emits a constrained JSON envelope, HPD parses it into `FunctionCallContent`, and the normal HPD function execution loop runs the tool.

## When To Use It

Use this path when:

- The app already owns a local ONNX Runtime GenAI model directory.
- The model can follow a tool-call JSON contract.
- Tool execution should stay inside HPD middleware, permissions, events, and result handling.

For general local chat without tools, use the normal [ONNX Runtime Provider](onnx-runtime.md) setup.

## Model Requirements

The model directory must be an ONNX Runtime GenAI model directory with files such as:

```text
genai_config.json
model.onnx
model.onnx.data
tokenizer.json
tokenizer_config.json
```

Small models can work when the prompt and schema are tight. A verified smoke target is a Qwen2.5 0.5B instruct model exported for ONNX Runtime GenAI CPU int4. In local testing, this model produced tool calls for simple arithmetic and supported repeated user turns when the agent prompted it directly.

Tiny models may still produce malformed suffixes, repeated braces, or weak final-answer behavior. HPD parses the first balanced JSON object so a correct tool-call envelope can still be accepted when the model emits extra trailing text.

## Enable The Adapter

Enable structured tool calling in the ONNX provider config:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OnnxRuntime;

var modelPath = Environment.GetEnvironmentVariable("ONNX_MODEL_PATH")
    ?? "/absolute/path/to/onnx-genai-model";

var agent = await new AgentBuilder()
    .WithName("local-tool-agent")
    .WithInstructions("Use tools when the user asks for arithmetic.")
    .WithOnnxRuntime(
        modelPath,
        options =>
        {
            options.EnableStructuredToolCalling = true;
            options.Temperature = 0;
            options.MaxLength = 128;
        })
    .BuildAsync();
```

When disabled, the ONNX provider behaves like normal local text generation.

## Add Tools

The ONNX adapter reads the same `ChatOptions.Tools` values used by HPD's normal tool path. Tools can come from native functions, generated harnesses, MCP tools, OpenAPI tools, subagents, or multi-agent capabilities.

Use the same tool registration style you use for any other provider. A generated harness example looks like this:

```csharp
using HPD.Agent;
using Microsoft.Extensions.AI;

public sealed class MathTools
{
    [AIFunction]
    public int Add(int a, int b) => a + b;
}

var agent = await new AgentBuilder()
    .WithOnnxRuntime(modelPath, options =>
    {
        options.EnableStructuredToolCalling = true;
        options.Temperature = 0;
        options.MaxLength = 128;
    })
    .WithToolHarness<MathTools>()
    .WithOptionsConfiguration(options =>
    {
        options.AllowMultipleToolCalls = false;
        options.ToolMode = ChatToolMode.RequireSpecific("Add");
        options.Temperature = 0;
        options.MaxOutputTokens = 96;
    })
    .BuildAsync();
```

Native functions, selected harness functions, MCP tools, OpenAPI tools, subagent tools, multi-agent tools, and collapsed harness containers use the same rule: if HPD registers them into `ChatOptions.Tools` as function declarations, the ONNX adapter can describe them to the local model.

The model-facing schema is taken from the generated or reflected `AIFunctionDeclaration.JsonSchema`; the ONNX adapter does not regenerate schemas.

## Request Flow

For a tool-capable ONNX turn:

1. HPD builds `ChatOptions.Tools`.
2. The ONNX structured adapter converts functions into compact tool JSON.
3. The adapter asks the local model for a JSON envelope:

```json
{
  "tool_call": {
    "name": "Add",
    "arguments": {
      "a": 2,
      "b": 3
    }
  }
}
```

4. HPD parses the envelope into `FunctionCallContent`.
5. The normal HPD function execution core runs the tool.
6. Tool events and results are emitted through the regular event stream.

The adapter respects `ChatToolMode.None`; when tool mode is `None`, it delegates to normal ONNX text generation even if tools are present.

## Live Smoke

Run the structured tool-calling smoke with a local ONNX Runtime GenAI model directory:

```bash
ONNX_MODEL_PATH=/absolute/path/to/qwen2.5-0.5b-instruct-cpu-int4 \
ONNX_TOOL_CALL_SMOKE=1 \
dotnet test test/HPD-Agent.Providers.Tests/HPD-Agent.Providers.Tests.csproj \
  --framework net10.0 \
  --filter "FullyQualifiedName~OnnxRuntimeStructuredToolCalling"
```

The live smoke covers:

- tool JSON conversion
- tool-call envelope parsing
- provider-level `FunctionCallContent`
- agent-level tool execution
- repeated user turns on the same local ONNX-backed agent

Useful variables:

| Variable | Purpose |
| --- | --- |
| `ONNX_MODEL_PATH` | ONNX Runtime GenAI model directory |
| `ONNX_TOOL_CALL_SMOKE` | Set to `1` to run live tool-call smokes |
| `ONNX_TOOL_CALL_PROMPT` | Override the default arithmetic smoke prompt |
| `ONNX_TOOL_CALL_MAX_LENGTH` | Provider max-length setting |
| `ONNX_TOOL_CALL_OUTPUT_TOKENS` | Request output token cap |
| `ONNX_TOOL_CALL_TIMEOUT_SECONDS` | Whole smoke timeout |
| `ONNX_TOOL_CALL_EVENT_TIMEOUT_SECONDS` | Tool-result event wait timeout |

## Limitations

Keep these boundaries in mind:

- Model quality matters. A tiny model may need direct prompts and deterministic generation settings.
- The first implementation supports one tool-call envelope per model turn.
- Streaming tool-call deltas are not first-class yet; structured tool turns are parsed after a completed generation.
- Provider metadata may remain conservative when capability depends on both config and model choice.
- Do not describe this as ONNX native function calling. The capability is HPD structured tool calling over local ONNX generation.

## Related Reading

- [ONNX Runtime Provider](onnx-runtime.md)
- [Tools, Functions, And Harnesses](../../concepts/tools-functions-and-harnesses.md)
- [Tool And Function Events](../events/tool-and-function-events.md)
- [Source Generation, AOT, And Trimming](../tools/source-generation-aot-and-trimming.md)
