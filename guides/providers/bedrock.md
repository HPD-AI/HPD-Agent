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
using Amazon.Runtime;

var agent = await new AgentBuilder()
    .WithBedrock(model: "anthropic.claude-3-5-sonnet-20240620-v1:0", region: "us-east-1")
    .BuildAsync();

var result = await agent.RunAsync("Write one sentence about Bedrock setup.");
Console.WriteLine(result.Text);
```

Runtime chat behavior such as model selection overrides, temperature, top-p, max output tokens, stop sequences, tools, structured output, and reasoning belongs in `ChatDefaults` or per-run `AgentRunConfig.Chat`:

```csharp
var agent = await new AgentBuilder()
    .WithBedrock(model: "anthropic.claude-3-5-sonnet-20240620-v1:0", region: "us-east-1")
    .WithChatDefaults(chat =>
    {
        chat.Temperature = 0.2;
        chat.TopP = 0.95;
        chat.MaxOutputTokens = 4096;
        chat.Reasoning = new()
        {
            Effort = ReasoningEffort.High
        };
    })
    .BuildAsync();
```

`BedrockProviderConfig` is for AWS-native client construction:

```csharp
var agent = await new AgentBuilder()
    .WithBedrock(
        model: "anthropic.claude-3-5-sonnet-20240620-v1:0",
        region: "us-east-1",
        configure: bedrock =>
        {
            bedrock.ProfileName = "hpd-dev";
            bedrock.ServiceUrl = "https://vpce-xxx.bedrock-runtime.us-east-1.vpce.amazonaws.com";
            bedrock.AuthenticationRegion = "us-east-1";
            bedrock.SigV4aSigningRegionSet = ["us-east-1", "us-west-2"];
            bedrock.UseFipsEndpoint = true;
            bedrock.UseDualstackEndpoint = true;
            bedrock.RequestTimeoutMs = 120000;
            bedrock.ConnectTimeoutMs = 5000;
            bedrock.MaxRetryAttempts = 4;
            bedrock.RetryMode = RequestRetryMode.Adaptive;
            bedrock.DefaultConfigurationMode = DefaultConfigurationMode.CrossRegion;
        })
    .BuildAsync();
```

The equivalent `Clients.Chat` shape is:

```json
{
  "Clients": {
    "Chat": {
      "ProviderKey": "bedrock",
      "ModelName": "anthropic.claude-3-5-sonnet-20240620-v1:0",
      "ProviderOptions": {
        "region": "us-east-1",
        "profileName": "hpd-dev",
        "serviceUrl": "https://vpce-xxx.bedrock-runtime.us-east-1.vpce.amazonaws.com",
        "authenticationRegion": "us-east-1",
        "sigV4aSigningRegionSet": ["us-east-1", "us-west-2"],
        "useFipsEndpoint": true,
        "useDualstackEndpoint": true,
        "requestTimeoutMs": 120000,
        "connectTimeoutMs": 5000,
        "maxRetryAttempts": 4,
        "retryMode": "Adaptive",
        "defaultConfigurationMode": "CrossRegion"
      }
    }
  }
}
```

## Provider Options

`BedrockProviderConfig` is for AWS SDK client construction. Production applications should normally lean on the AWS SDK credential chain or host identity, and only set explicit credentials when the host environment cannot provide them.

### Identity And Region

| Option | Purpose |
| --- | --- |
| `Region` | AWS region where Bedrock Runtime is hosted. |
| `AccessKeyId` | Explicit AWS access key id. Prefer the AWS SDK credential chain when possible. |
| `SecretAccessKey` | Explicit AWS secret access key. Prefer the AWS SDK credential chain when possible. |
| `SessionToken` | AWS session token for temporary credentials. |
| `ProfileName` | Shared AWS profile name from local AWS config/credentials files. |

### Endpoint And Signing

| Option | Purpose |
| --- | --- |
| `ServiceUrl` | Custom Bedrock Runtime endpoint URL, such as a VPC endpoint. |
| `AuthenticationRegion` | AWS signing region, useful when it cannot be inferred from a custom endpoint. |
| `AuthenticationServiceName` | AWS signing service name. |
| `AuthSchemePreference` | Ordered list of preferred AWS auth schemes, such as `sigv4` or `sigv4a`. |
| `SigV4aSigningRegionSet` | AWS SigV4a signing region set. |
| `UseFipsEndpoint` | Uses FIPS-compliant endpoints when available. |
| `UseDualstackEndpoint` | Uses dual-stack endpoints when available. |
| `UseHttp` | Uses HTTP instead of HTTPS. |
| `IgnoreConfiguredEndpointUrls` | Ignores endpoint URLs configured outside this provider config. |
| `DisableHostPrefixInjection` | Disables SDK host prefix injection for custom/local endpoint scenarios. |
| `EndpointDiscoveryEnabled` | Enables AWS endpoint discovery. |

### Timeouts And Retries

| Option | Purpose |
| --- | --- |
| `RequestTimeoutMs` | Overall request timeout in milliseconds. |
| `ConnectTimeoutMs` | Connection timeout in milliseconds. |
| `MaxRetryAttempts` | Maximum number of retry attempts for failed requests. |
| `RetryMode` | AWS SDK retry mode, such as `Standard` or `Adaptive`. |
| `DefaultConfigurationMode` | AWS SDK default configuration mode, such as `Standard`, `InRegion`, or `CrossRegion`. |
| `MaxStaleConnectionRetries` | Maximum retry attempts for stale HTTP connections. |
| `ThrottleRetries` | Enables AWS SDK retry throttling. |
| `FastFailRequests` | Fails quickly when retry capacity is unavailable. |
| `ResignRetries` | Re-signs requests on retry. |

### HTTP Pipeline

| Option | Purpose |
| --- | --- |
| `DisableRequestCompression` | Disables SDK request compression. |
| `RequestMinCompressionSizeBytes` | Minimum request size in bytes before compression is considered. |
| `CacheHttpClient` | Caches HTTP clients created by the AWS SDK. |
| `HttpClientCacheSize` | Maximum AWS SDK HTTP client cache size. |
| `ProxyHost` | Proxy host used by the AWS SDK HTTP pipeline. |
| `ProxyPort` | Proxy port used by the AWS SDK HTTP pipeline. |
| `MaxConnectionsPerServer` | Maximum concurrent HTTP connections per server. |
| `AllowAutoRedirect` | Allows automatic HTTP redirects. |

### Diagnostics And Transfer

| Option | Purpose |
| --- | --- |
| `ClientAppId` | AWS SDK client application identifier. |
| `LogResponse` | Logs response bodies through AWS SDK logging. |
| `BufferSize` | AWS SDK transfer buffer size. |
| `ProgressUpdateIntervalMs` | AWS SDK progress update interval in milliseconds. |
| `LogMetrics` | Enables AWS SDK metrics logging. |
| `DisableLogging` | Disables AWS SDK logging. |

`ReadWriteTimeout` is not exposed because the current AWS SDK only exposes it on the .NET Framework asset, not the net8/net9/net10 assets HPD targets.

Reasoning is passed through `ChatOptions.Reasoning` to Bedrock Converse extended thinking when the selected model supports it. Models without extended thinking support return a Bedrock API error.
