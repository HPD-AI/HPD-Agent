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

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "ollama",
      "ModelName": "llama3.2",
      "Endpoint": "http://localhost:11434"
    }
  }
}
```

Endpoint resolution in fluent setup checks:

1. the explicit `endpoint` argument
2. `OLLAMA_ENDPOINT`
3. `OLLAMA_HOST`
4. `http://localhost:11434`

## Caveats

Validation checks endpoint URI shape. A successful build still requires a reachable Ollama server and a pulled model.

`OLLAMA_ENDPOINT` and `OLLAMA_HOST` are used by fluent `.WithOllama(...)`. JSON/config setup should include `Endpoint` when the server is not at `http://localhost:11434`.

