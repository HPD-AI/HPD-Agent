# Replicate

The Replicate provider uses provider key `replicate` and the `HPD-Agent.Providers.Replicate` package. It registers image generation only. `ModelName` is a Replicate `owner/model` id, such as `black-forest-labs/flux-schnell`.

The provider package is currently `net10.0` only because the underlying Replicate SDK targets `net10.0`.

Set an API key:

```bash
export REPLICATE_API_KEY="..."
```

`REPLICATE_API_TOKEN` is also registered as an alias. The default endpoint is `https://api.replicate.com/v1/`. Pass `endpoint` to `WithReplicateImageGeneration(...)`, or set `Endpoint` in `ClientProviderConfig`, when you need a compatible proxy.

Use fluent image setup:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Replicate;

var agent = await new AgentBuilder()
    .WithReplicateImageGeneration(
        model: "black-forest-labs/flux-schnell",
        configure: options =>
        {
            options.Input = new()
            {
                ["aspect_ratio"] = "16:9",
                ["num_inference_steps"] = 4
            };
        })
    .BuildAsync();
```

The equivalent family config is:

```json
{
  "Clients": {
    "ImageGeneration": {
      "ProviderKey": "replicate",
      "ModelName": "black-forest-labs/flux-schnell"
    }
  }
}
```

## Configuration

`ReplicateProviderConfig.Input` passes model-specific input fields to Replicate along with the prompt supplied in the image generation request:

```csharp
var agent = await new AgentBuilder()
    .WithReplicateImageGeneration(
        model: "black-forest-labs/flux-schnell",
        configure: options =>
        {
            options.Input = new()
            {
                ["aspect_ratio"] = "1:1",
                ["output_format"] = "webp"
            };
            options.TimeoutSeconds = 120;
            options.PollingIntervalSeconds = 2;
        })
    .BuildAsync();
```

If you want to store the owner separately, set `ModelOwner` and use only the model name in `ModelName`.

## Caveats

This package does not expose Replicate's general predictions API as a new HPD provider family. It only adapts model predictions that return image URLs into `IImageGenerator`.

Replicate chat, embeddings, trainings, deployments, files, and arbitrary prediction runs are not registered by this provider.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
