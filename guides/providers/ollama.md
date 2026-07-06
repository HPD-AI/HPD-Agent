# Ollama

The Ollama provider uses provider key `ollama` and the `HPD-Agent.Providers.Ollama` package. It does not require an API key. `ModelName` is a local Ollama model tag, such as `llama3.2` or `llama3:8b`.

Start Ollama and make sure the model is available locally:

```bash
ollama pull llama3.2
```

Use fluent setup:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Ollama;

var agent = await new AgentBuilder()
    .WithOllama(model: "llama3.2")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about local model setup.");
Console.WriteLine(result.Text);
```

Provider construction options stay small. Configure the Ollama server connection on the provider, and put model behavior on chat defaults or per-run chat config:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Ollama;

var agent = await new AgentBuilder()
    .WithOllama(
        model: "qwen3:latest",
        endpoint: "http://localhost:11434",
        configure: ollama =>
        {
            ollama.TimeoutMs = 120_000;
        })
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.MaxOutputTokens = 1024;
        chat.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.Medium
        };
    })
    .WithOllamaChatRequestOptions(ollama =>
    {
        ollama.KeepAlive = "10m";
        ollama.NumCtx = 8192;
        ollama.NumGpu = 99;
        ollama.UseMlock = true;
    })
    .BuildAsync();
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "ollama",
      "ModelName": "llama3.2",
      "Endpoint": "http://localhost:11434",
      "ChatDefaults": {
        "Temperature": 0.2,
        "MaxOutputTokens": 1024,
        "Reasoning": {
          "Effort": "Medium"
        },
        "AdditionalProperties": {
          "keep_alive": "10m",
          "num_ctx": 8192,
          "num_gpu": 99,
          "use_mlock": true
        }
      },
      "ProviderOptions": {
        "TimeoutMs": 120000
      }
    }
  }
}
```

Endpoint resolution in fluent setup checks:

1. the explicit `endpoint` argument
2. `OLLAMA_ENDPOINT`
3. `OLLAMA_HOST`
4. `http://localhost:11434`

## Runtime Options

Use `ChatDefaults` or `AgentRunConfig.Chat` for generic model behavior:

- `ModelId`
- `Temperature`
- `TopP`
- `TopK`
- `MaxOutputTokens`
- `FrequencyPenalty`
- `PresencePenalty`
- `Seed`
- `StopSequences`
- `ResponseFormat`
- `Reasoning`

Use `OllamaChatRequestOptions` for Ollama-only request properties:

| Option | Ollama key | Meaning |
| --- | --- | --- |
| `KeepAlive` | `keep_alive` | How long Ollama keeps the model loaded after the request, such as `10m`, `1h`, or `-1`. |
| `Template` | `template` | Prompt template override for this request. This overrides the template defined in the model file. |
| `NumCtx` | `num_ctx` | Context window size used by the model. |
| `NumGqa` | `num_gqa` | Grouped-query-attention group count required by some model architectures. |
| `NumGpu` | `num_gpu` | Number of layers to offload to GPU. |
| `MainGpu` | `main_gpu` | Main GPU index for small tensors when multiple GPUs are available. |
| `NumBatch` | `num_batch` | Maximum batch size for prompt processing. |
| `NumThread` | `num_thread` | Number of CPU threads used for generation. Ollama auto-detects when unset. |
| `NumKeep` | `num_keep` | Number of tokens to keep from the initial prompt. Ollama supports `-1` to keep all. |
| `MiroStat` | `mirostat` | Enables Mirostat sampling: `0` disabled, `1` Mirostat, `2` Mirostat 2.0. |
| `MiroStatEta` | `mirostat_eta` | Mirostat learning rate. Higher values react more quickly to feedback. |
| `MiroStatTau` | `mirostat_tau` | Mirostat target entropy. Lower values are more focused; higher values add variety. |
| `RepeatLastN` | `repeat_last_n` | Number of prior tokens considered for repetition penalties. Ollama supports `0` to disable and `-1` for the full context. |
| `RepeatPenalty` | `repeat_penalty` | Repetition penalty strength. Higher values penalize repetition more strongly. |
| `MinP` | `min_p` | Minimum probability threshold relative to the most likely token. |
| `TypicalP` | `typical_p` | Locally typical sampling value. Lower values make output more conservative. |
| `TfsZ` | `tfs_z` | Tail-free sampling value. A value near `1` disables the setting. |
| `PenalizeNewline` | `penalize_newline` | Whether newline tokens are penalized during repetition control. |
| `UseMmap` | `use_mmap` | Whether model weights are memory-mapped. |
| `UseMlock` | `use_mlock` | Whether to lock model memory to avoid swapping. |
| `LowVram` | `low_vram` | Enables low-VRAM mode. |
| `F16kv` | `f16_kv` | Enables f16 key/value cache. |
| `LogitsAll` | `logits_all` | Returns logits for all tokens, not only the last token. |
| `VocabOnly` | `vocab_only` | Loads only the vocabulary, not model weights. |
| `Numa` | `numa` | Enables NUMA support. |

Sampling values that already exist on `ChatRunConfig`, such as `Temperature`, `TopP`, `TopK`, `MaxOutputTokens`, `Seed`, `FrequencyPenalty`, `PresencePenalty`, and `StopSequences`, stay on `ChatDefaults` or `AgentRunConfig.Chat`.

## Caveats

Validation checks endpoint URI shape. A successful build still requires a reachable Ollama server and a pulled model.

`OLLAMA_ENDPOINT` and `OLLAMA_HOST` are used by fluent `.WithOllama(...)`. JSON/config setup should include `Endpoint` when the server is not at `http://localhost:11434`.
