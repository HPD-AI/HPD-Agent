# Microsoft.Extensions.AI Middleware Integration Example

The AgentBuilder now supports Microsoft.Extensions.AI middleware for enhanced chat client functionality.

## Example Usage

```csharp
// Basic agent with middleware
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging()                          // MS.Extensions.AI logging middleware
    .WithCaching()                          // MS.Extensions.AI caching middleware  
    .WithOptionsConfiguration(options => {  // MS.Extensions.AI options configuration
        options.Temperature = 0.7f;
        options.MaxOutputTokens = 1000;
    })
    .WithOpenTelemetry()                    // Already implemented telemetry
    .WithPlugin<SearchPlugin>()             // Existing plugin system
    .WithMCP("manifest.json")               // Existing MCP support
    .Build();
```

## Available Middleware Methods

### WithCaching(IDistributedCache? cache = null, Action<DistributedCachingChatClient>? configure = null)
- Adds distributed caching to reduce redundant LLM calls
- Automatically resolves IDistributedCache from service provider if not provided
- Logs warning if no cache implementation is available

### WithLogging(ILoggerFactory? loggerFactory = null, bool includeChats = true, bool includeFunctions = true, Action<LoggingChatClient>? configureChat = null, Action<LoggingAiFunctionFilter>? configureFunction = null)
- **Comprehensive logging** - logs both chat operations AND function invocations by default
- Uses structured logging with proper log levels and categories
- Uses AgentBuilder's logger factory by default, falls back to console logging
- Supports granular control over what gets logged

### WithFunctionLogging(ILoggerFactory? loggerFactory = null, Action<LoggingAiFunctionFilter>? configure = null)
- **Advanced method** - adds only function invocation logging for explicit control
- Perfect for scenarios where you want function logging without chat logging

### WithMessageReducer(object? reducer = null, Action<object>? configure = null)
- Placeholder for message reduction functionality
- Will be implemented when ReducingChatClient becomes available in Microsoft.Extensions.AI
- Currently logs a warning and returns the original client

### WithOptionsConfiguration(Action<ChatOptions> configureOptions)
- Adds options configuration middleware
- Allows dynamic modification of ChatOptions for each request

## Comprehensive Logging Examples

```csharp
// 🎯 Simple - logs everything (90% of users want this)
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging()  // Logs both chats AND function calls
    .Build();

// 🔍 Only chat logging, no function logging
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging(includeFunctions: false)
    .Build();

// ⚙️ Only function logging, no chat logging
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging(includeChats: false)
    .Build();

// 🛠️ Custom configuration with fine-grained control
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging(
        configureFunction: filter => {
            filter.LogLevel = LogLevel.Trace;
            filter.LogArguments = false;  // Don't log sensitive args
            filter.LogResults = true;
        })
    .Build();

// 🚀 Advanced users - explicit function logging control
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithFunctionLogging(filter => {
        filter.LogLevel = LogLevel.Information;
        filter.LogArguments = false;  // Security: don't log function args
    })
    .Build();

// 🏗️ Complete example with all middleware
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging()                          // Comprehensive logging
    .WithCaching()                          // Response caching
    .WithOptionsConfiguration(opts => {     // Dynamic options
        opts.Temperature = 0.7f;
        opts.MaxOutputTokens = 2000;
    })
    .WithOpenTelemetry()                    // Telemetry
    .WithPlugin<SearchPlugin>()             // Plugins
    .WithMCP("manifest.json")               // MCP tools
    .Build();
```

## Middleware Pipeline Order

Middleware is applied in the order it's configured:

```csharp
var agent = AgentBuilder.Create()
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithLogging()          // Applied first (innermost)
    .WithCaching()          // Applied second
    .WithOptionsConfiguration(opts => opts.Temperature = 0.5f) // Applied third (outermost)
    .Build();
```

The final pipeline will be: `ConfigureOptions -> Caching -> Logging -> BaseClient`

## Integration with Existing Features

The middleware system is fully compatible with existing AgentBuilder features:

- **OpenTelemetry**: Still works as before, wraps the base client directly
- **Plugin System**: Unchanged, plugins are merged into ChatOptions
- **MCP Support**: Unchanged, MCP tools are loaded separately  
- **Filter System**: Unchanged, operates at the Agent level
- **Memory Systems**: Unchanged, operates via prompt filters

## Service Provider Integration

When a service provider is available, middleware will automatically resolve dependencies:

```csharp
// Setup in DI container
services.AddDistributedMemoryCache();
services.AddLogging();

var agent = AgentBuilder.Create()
    .WithServiceProvider(serviceProvider)
    .WithProvider(ChatProvider.OpenAI, "gpt-4")
    .WithCaching()  // Will find IDistributedCache from DI
    .WithLogging()  // Will find ILoggerFactory from DI
    .Build();
```

## Backward Compatibility

All existing AgentBuilder code continues to work unchanged. The middleware system is purely additive and doesn't affect existing functionality.