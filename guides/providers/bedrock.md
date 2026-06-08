# Amazon Bedrock

The Bedrock provider uses provider key `bedrock` and the `HPD-Agent.Providers.Bedrock` package. `ModelName` is an Amazon Bedrock model id, such as an Anthropic Claude model id in Bedrock format.

Set a region:

```bash
export AWS_REGION="us-east-1"
# or
export AWS_DEFAULT_REGION="us-east-1"
```

Credentials usually come from the AWS SDK default credential chain: environment variables such as `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN`, shared AWS profile, IAM role, or other SDK-supported sources. Use your normal AWS credential setup for the host environment.

Use fluent setup first:

```csharp
using HPD.Agent;
using HPD.Agent.Providers.Bedrock;

var agent = await new AgentBuilder()
    .WithBedrock(model: "anthropic.claude-3-5-sonnet-20240620-v1:0", region: "us-east-1")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Bedrock setup.");
Console.WriteLine(result.Text);
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "bedrock",
      "ModelName": "anthropic.claude-3-5-sonnet-20240620-v1:0",
      "ProviderOptionsJson": "{\"region\":\"us-east-1\"}"
    }
  }
}
```

Optional explicit values exist for access key id, secret access key, session token, region, and service URL, but production applications should normally lean on the AWS SDK credential chain or host identity. Bedrock credential aliases are registered, but current client creation does not copy explicit credential fields from `ISecretResolver` into `BedrockProviderConfig`.

## Caveats

Validation checks model id, region, option ranges, guardrails, and explicit credential pairing. A successful build also depends on AWS account permissions for the selected Bedrock model and region.

