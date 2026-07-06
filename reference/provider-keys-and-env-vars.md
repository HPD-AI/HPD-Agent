# Provider Keys And Environment Variables

This page lists provider keys and public environment variable aliases for chat setup.

| Package | Provider key | Model means | Required or common environment variables |
| --- | --- | --- | --- |
| `HPD-Agent.Providers.OpenAI` | `openai` | OpenAI model id | `OPENAI_API_KEY`; optional custom endpoint by convention: `OPENAI_ENDPOINT` |
| `HPD-Agent.Providers.OpenAI` | `azure-openai` | Azure OpenAI deployment name | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` |
| `HPD-Agent.Providers.AzureAI` | `azure-ai` | Azure AI Foundry/Projects or Azure OpenAI deployment name | `AZURE_AI_ENDPOINT`; `AZURE_AI_API_KEY` only when not using OAuth |
| `HPD-Agent.Providers.Anthropic` | `anthropic` | Claude model id | `ANTHROPIC_API_KEY` |
| `HPD-Agent.Providers.Cohere` | `cohere` | Cohere chat model id or embedding model id | `COHERE_API_KEY` |
| `HPD-Agent.Providers.DashScope` | `dashscope` | DashScope/Qwen chat model id or embedding model id | `DASHSCOPE_API_KEY`; aliases: `QWEN_API_KEY`, `DASHSCOPE_KEY`; set `Endpoint` or `baseAddress` for a custom base URL |
| `HPD-Agent.Providers.Cerebras` | `cerebras` | Cerebras OpenAI-compatible chat model id | `CEREBRAS_API_KEY`; optional endpoint aliases: `CEREBRAS_ENDPOINT`, `CEREBRAS_BASE_URL` |
| `HPD-Agent.Providers.DeepSeek` | `deepseek` | DeepSeek OpenAI-compatible chat model id | `DEEPSEEK_API_KEY`; optional endpoint aliases: `DEEPSEEK_ENDPOINT`, `DEEPSEEK_BASE_URL` |
| `HPD-Agent.Providers.DeepInfra` | `deepinfra` | DeepInfra OpenAI-compatible chat model id | `DEEPINFRA_API_KEY`; optional endpoint aliases: `DEEPINFRA_ENDPOINT`, `DEEPINFRA_BASE_URL` |
| `HPD-Agent.Providers.Fireworks` | `fireworks` | Fireworks chat model id | `FIREWORKS_API_KEY`; optional endpoint aliases: `FIREWORKS_ENDPOINT`, `FIREWORKS_BASE_URL` |
| `HPD-Agent.Providers.SambaNova` | `sambanova` | SambaNova OpenAI-compatible chat model id | `SAMBANOVA_API_KEY`; optional endpoint aliases: `SAMBANOVA_ENDPOINT`, `SAMBANOVA_BASE_URL` |
| `HPD-Agent.Providers.Hyperbolic` | `hyperbolic` | Hyperbolic OpenAI-compatible chat model id | `HYPERBOLIC_API_KEY`; optional endpoint aliases: `HYPERBOLIC_ENDPOINT`, `HYPERBOLIC_BASE_URL` |
| `HPD-Agent.Providers.OVHcloud` | `ovhcloud` | OVHcloud AI Endpoints OpenAI-compatible chat model id | `OVHCLOUD_API_KEY`; optional endpoint aliases: `OVHCLOUD_ENDPOINT`, `OVHCLOUD_BASE_URL` |
| `HPD-Agent.Providers.Nscale` | `nscale` | Nscale OpenAI-compatible chat model id | `NSCALE_API_KEY`; optional endpoint aliases: `NSCALE_ENDPOINT`, `NSCALE_BASE_URL` |
| `HPD-Agent.Providers.Venice` | `venice` | Venice.ai OpenAI-compatible chat model id | `VENICE_API_KEY`; optional endpoint aliases: `VENICE_ENDPOINT`, `VENICE_BASE_URL` |
| `HPD-Agent.Providers.Perplexity` | `perplexity` | Perplexity Sonar chat model id | `PERPLEXITY_API_KEY`; optional endpoint aliases: `PERPLEXITY_ENDPOINT`, `PERPLEXITY_BASE_URL` |
| `HPD-Agent.Providers.LMStudio` | `lmstudio` | Loaded LM Studio local model id | No API key required by default; optional aliases: `LMSTUDIO_API_KEY`, `LM_STUDIO_API_KEY`; endpoint aliases: `LMSTUDIO_ENDPOINT`, `LMSTUDIO_BASE_URL`, `LMSTUDIO_API_BASE`, `LM_STUDIO_ENDPOINT`, `LM_STUDIO_BASE_URL`, `LM_STUDIO_API_BASE` |
| `HPD-Agent.Providers.Nebius` | `nebius` | Nebius Token Factory chat model id | `NEBIUS_API_KEY`; optional endpoint aliases: `NEBIUS_ENDPOINT`, `NEBIUS_BASE_URL` |
| `HPD-Agent.Providers.NvidiaNim` | `nvidia-nim` | NVIDIA NIM chat model id | `NVIDIA_API_KEY` or `NVIDIA_NIM_API_KEY`; optional endpoint aliases: `NVIDIA_NIM_ENDPOINT`, `NVIDIA_NIM_BASE_URL`, `NVIDIA_ENDPOINT`, `NVIDIA_BASE_URL` |
| `HPD-Agent.Providers.SiliconFlow` | `siliconflow` | SiliconFlow OpenAI-compatible chat model id | `SILICONFLOW_API_KEY`; optional endpoint aliases: `SILICONFLOW_ENDPOINT`, `SILICONFLOW_BASE_URL` |
| `HPD-Agent.Providers.Scaleway` | `scaleway` | Scaleway Generative APIs chat model id | `SCW_SECRET_KEY`, `SCALEWAY_API_KEY`, or `SCW_API_KEY`; optional endpoint aliases: `SCALEWAY_ENDPOINT`, `SCALEWAY_BASE_URL`, `SCW_ENDPOINT`, `SCW_BASE_URL` |
| `HPD-Agent.Providers.Zai` | `zai` | Z.AI chat model id | `ZAI_API_KEY`, `Z_AI_API_KEY`, or `BIGMODEL_API_KEY`; optional endpoint aliases: `ZAI_ENDPOINT`, `ZAI_BASE_URL`, `Z_AI_ENDPOINT`, `Z_AI_BASE_URL`, `BIGMODEL_ENDPOINT`, `BIGMODEL_BASE_URL` |
| `HPD-Agent.Providers.MiniMax` | `minimax` | MiniMax chat model id | `MINIMAX_API_KEY`; optional endpoint aliases: `MINIMAX_ENDPOINT`, `MINIMAX_BASE_URL`, `MINIMAX_API_BASE` |
| `HPD-Agent.Providers.Together` | `together` | Together chat model id or embedding model id | `TOGETHER_API_KEY`; set `Endpoint` for a custom base URL |
| `HPD-Agent.Providers.Xai` | `xai` | xAI chat model id | `XAI_API_KEY`; optional endpoint aliases: `XAI_ENDPOINT`, `XAI_BASE_URL` |
| `HPD-Agent.Providers.Groq` | `groq` | Groq chat model id | `GROQ_API_KEY`; optional endpoint alias: `GROQ_ENDPOINT` |
| `HPD-Agent.Providers.Moonshot` | `moonshot` | Moonshot/Kimi chat model id | `MOONSHOT_API_KEY`; alias: `KIMI_API_KEY`; optional endpoint aliases: `MOONSHOT_ENDPOINT`, `MOONSHOT_BASE_URL`, `KIMI_ENDPOINT`, `KIMI_BASE_URL` |
| `HPD-Agent.Providers.Replicate` | `replicate` | Replicate image model id in `owner/model` format | `REPLICATE_API_KEY` or `REPLICATE_API_TOKEN` |
| `HPD-Agent.Providers.GoogleAI` | `google-ai` | Gemini model id | registered aliases: `GOOGLE_API_KEY`, `GEMINI_API_KEY`, or `GOOGLE_AI_API_KEY` |
| `HPD-Agent.Providers.Ollama` | `ollama` | Local Ollama model tag | Fluent setup can read `OLLAMA_ENDPOINT` or `OLLAMA_HOST`; JSON/config should set `Endpoint` when not using localhost |
| `HPD-Agent.Providers.HuggingFace` | `huggingface` | Hugging Face repository id | `HUGGINGFACE_API_KEY` or `HF_TOKEN` |
| `HPD-Agent.Providers.Mistral` | `mistral` | Mistral model id | `MISTRAL_API_KEY` |
| `HPD-Agent.Providers.Bedrock` | `bedrock` | Amazon Bedrock model id | `AWS_REGION` or `AWS_DEFAULT_REGION`; credentials usually come from the AWS SDK default credential chain |
| `HPD-Agent.Providers.OnnxRuntime` | `onnx-runtime` | Local ONNX Runtime GenAI model directory | `ONNX_MODEL_PATH` for provider-config setup |
| `HPD-Agent.Providers.Audio.OpenAI` | `openai` | Audio model id for STT/TTS/realtime families | `OPENAI_API_KEY`; optional endpoint aliases: `OPENAI_ENDPOINT`, `OPENAI_BASE_URL` |
| `HPD-Agent.Providers.Audio.ElevenLabs` | `elevenlabs` | ElevenLabs STT/TTS model or voice setup | `ELEVENLABS_API_KEY` |
| `HPD-Agent.Providers.OpenRouter` | `openrouter` | OpenRouter model id | No fluent `AgentBuilder` helper in the current package; `OPENROUTER_API_KEY` |

## Configuration Keys

When configuration is available, the default resolver checks provider-scoped keys. Prefer exact provider keys:

```text
openai:ApiKey
Providers:openai:ApiKey
azure-openai:Endpoint
Providers:azure-openai:Endpoint
```

Explicit values in `ClientProviderConfig` or fluent setup take precedence over resolver lookup.

Bedrock credential env variables such as `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` are accepted by the AWS SDK default credential chain and are also registered as `bedrock:*` aliases. `bedrock:Region` resolves `AWS_REGION` and `AWS_DEFAULT_REGION`.

## Live Smoke Gates

Live provider smokes are opt-in so ordinary test runs do not spend provider quota or require local model assets.

| Smoke | Required variables |
| --- | --- |
| OpenAI text-to-speech | `HPD_AUDIO_LIVE_SMOKE=1`, `OPENAI_API_KEY` |
| OpenAI realtime agent turn | `HPD_REALTIME_LIVE_SMOKE=1`, `OPENAI_API_KEY` |
| ElevenLabs text-to-speech | `HPD_AUDIO_LIVE_SMOKE=1`, `ELEVENLABS_API_KEY` |
| ElevenLabs realtime speech-to-text | `ELEVENLABS_API_KEY` and a caller/test that sends PCM audio through the STT streaming path |
| ONNX Runtime local inference | `ONNX_MODEL_PATH` |

## Azure Terms

Use precise Azure wording:

- `azure-openai` is the traditional Azure OpenAI resource path. Its `ModelName` is a deployment name, and setup requires an endpoint and API key.
- `azure-ai` is the current Azure AI Projects / Foundry path. It also supports traditional Azure OpenAI endpoints. Foundry/Projects endpoints use `services.ai.azure.com` with `/api/projects/` and require OAuth; passing an API key for that endpoint shape is invalid. `AuthMode.Auto` uses an API key when one is configured and otherwise falls back to `DefaultAzureCredential`.

## Setup Caveat

Some providers validate `ApiKey`, `Endpoint`, or `Region` before deferred secret resolution. Keep provider setup examples explicit when you need self-contained configuration, and use environment aliases when deployment configuration owns the secret values.
