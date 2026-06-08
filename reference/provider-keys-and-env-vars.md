# Provider Keys And Environment Variables

This page lists provider keys and public environment variable aliases for chat setup.

| Package | Provider key | Model means | Required or common environment variables |
| --- | --- | --- | --- |
| `HPD-Agent.Providers.OpenAI` | `openai` | OpenAI model id | `OPENAI_API_KEY`; optional custom endpoint by convention: `OPENAI_ENDPOINT` |
| `HPD-Agent.Providers.OpenAI` | `azure-openai` | Azure OpenAI deployment name | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` |
| `HPD-Agent.Providers.AzureAI` | `azure-ai` | Azure AI Foundry/Projects or Azure OpenAI deployment name | `AZURE_AI_ENDPOINT`; `AZURE_AI_API_KEY` only when not using OAuth |
| `HPD-Agent.Providers.Anthropic` | `anthropic` | Claude model id | `ANTHROPIC_API_KEY` |
| `HPD-Agent.Providers.GoogleAI` | `google-ai` | Gemini model id | registered aliases: `GOOGLE_API_KEY` or `GEMINI_API_KEY`; convention-supported: `GOOGLE_AI_API_KEY` |
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

Bedrock credential env variables such as `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` are accepted by the AWS SDK default credential chain and are also registered as `bedrock:*` aliases. Current client creation does not copy explicit credential fields from `ISecretResolver` into `BedrockProviderConfig`; use normal AWS SDK credential configuration or explicit typed config.

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
- `azure-ai` is the current Azure AI Projects / Foundry path. It also supports traditional Azure OpenAI endpoints. Foundry/Projects endpoints use `services.ai.azure.com` with `/api/projects/` and require OAuth; passing an API key for that endpoint shape is invalid.

## Setup Caveat

Some providers validate `ApiKey`, `Endpoint`, or `Region` before deferred secret resolution. Keep provider setup examples explicit when you need self-contained configuration, and use environment aliases when deployment configuration owns the secret values.
