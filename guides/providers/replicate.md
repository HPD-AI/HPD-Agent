# Replicate

The Replicate provider uses provider key `replicate` and the `HPD-Agent.Providers.Replicate` package. It registers image generation only. `ModelName` is a Replicate `owner/model` id, such as `black-forest-labs/flux-schnell`.

The provider package is currently `net10.0` only because the underlying Replicate SDK targets `net10.0`.

Set an API key:

```bash
export REPLICATE_API_KEY="..."
```

`REPLICATE_API_TOKEN` is also registered as an alias. The default endpoint is `https://api.replicate.com/v1/`. Pass `endpoint` to `WithReplicateImageGeneration(...)`, or set `Endpoint` in `ImageGenerationClientConfig`, when you need a compatible proxy.

Use fluent image setup:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Replicate;

var agent = await new AgentBuilder()
    .WithReplicateImageGeneration(
        model: "black-forest-labs/flux-schnell",
        configureOptions: options =>
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
  "clients": {
    "imageGeneration": {
      "provider": {
        "key": "replicate"
      },
      "modelName": "black-forest-labs/flux-schnell"
    }
  }
}
```

## Configuration

`ReplicateImageOptions.Input` passes model-specific input fields to Replicate along with the prompt supplied in the image generation request:

```csharp
var agent = await new AgentBuilder()
    .WithReplicateImageGeneration(
        model: "black-forest-labs/flux-schnell",
        configureOptions: options =>
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

If you want to store the owner separately, set `ModelOwner` in `ReplicateProviderConfig` and use only the model name in `ModelName`.

`ReplicateProviderConfig` is for construction-level settings such as `ModelOwner`. Pass it through `configureClient`; pass model predictions and polling controls through `configureOptions`.

## Caveats

This package does not expose Replicate's general predictions API as a new HPD provider family. It only adapts model predictions that return image URLs into `IImageGenerator`.

Replicate chat, embeddings, trainings, deployments, files, and arbitrary prediction runs are not registered by this provider.

## Related Reading

- [Provider Setup Overview](overview.md)
- [Provider Families](../../reference/provider-families.md)
- [Provider Keys And Environment Variables](../../reference/provider-keys-and-env-vars.md)
