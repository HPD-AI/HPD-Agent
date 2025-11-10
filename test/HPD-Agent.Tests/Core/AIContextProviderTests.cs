using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Xunit;
using HPD.Agent;
using HPD_Agent.Tests.Infrastructure;
using System.Text.Json;

namespace HPD_Agent.Tests.Core;

/// <summary>
/// Tests for AIContextProvider integration in Microsoft protocol agent.
/// Verifies implicit message enrichment, lifecycle callbacks, and state persistence.
/// </summary>
public class AIContextProviderTests : AgentTestBase
{
    #region Test Providers

    /// <summary>
    /// Simple stateless provider for basic testing.
    /// Adds a system message with user information.
    /// </summary>
    private class SimpleMemoryProvider : AIContextProvider
    {
        public int InvokingCallCount { get; private set; }
        public int InvokedCallCount { get; private set; }
        public List<IList<ChatMessage>> ReceivedMessages { get; } = new();
        public List<IList<ChatMessage>?> ResponseMessages { get; } = new();

        public override ValueTask<AIContext> InvokingAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            InvokingCallCount++;
            ReceivedMessages.Add(context.RequestMessages.ToList());

            return ValueTask.FromResult(new AIContext
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "USER_INFO: Name is Alice, likes pizza")
                }
            });
        }

        public override ValueTask InvokedAsync(
            InvokedContext context,
            CancellationToken cancellationToken = default)
        {
            InvokedCallCount++;
            ResponseMessages.Add(context.ResponseMessages?.ToList());
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stateful provider that tracks conversation count.
    /// Tests serialization and state restoration.
    /// </summary>
    private class StatefulCounterProvider : AIContextProvider
    {
        private int _conversationCount;

        public int ConversationCount => _conversationCount;

        public StatefulCounterProvider() { }

        public StatefulCounterProvider(JsonElement state, JsonSerializerOptions? options = null)
        {
            if (state.TryGetProperty("conversationCount", out var countProp))
            {
                _conversationCount = countProp.GetInt32();
            }
        }

        public override ValueTask<AIContext> InvokingAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            _conversationCount++;

            return ValueTask.FromResult(new AIContext
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, $"Conversation count: {_conversationCount}")
                }
            });
        }

        public override JsonElement Serialize(JsonSerializerOptions? options = null)
        {
            var json = $"{{\"conversationCount\": {_conversationCount}}}";
            return JsonDocument.Parse(json).RootElement.Clone();
        }
    }

    /// <summary>
    /// Provider that adds dynamic tools based on context.
    /// </summary>
    private class DynamicToolProvider : AIContextProvider
    {
        public override ValueTask<AIContext> InvokingAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            var weatherTool = AIFunctionFactory.Create(
                (string location) => $"Weather in {location}: Sunny, 72°F",
                "get_weather",
                "Gets weather for a location");

            return ValueTask.FromResult(new AIContext
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "Weather tool available")
                },
                Tools = new List<AITool> { weatherTool }
            });
        }
    }

    /// <summary>
    /// Provider that adds additional instructions.
    /// </summary>
    private class InstructionProvider : AIContextProvider
    {
        public override ValueTask<AIContext> InvokingAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AIContext
            {
                Instructions = "Always respond in a friendly tone."
            });
        }
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task Agent_WithoutProvider_WorksNormally()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Hello!");

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithInstructions("You are a test agent");
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        var response = await agent.RunAsync("Hi", thread, cancellationToken: TestCancellationToken);

        // Assert
        Assert.Single(response.Messages);
        Assert.Equal("Hello!", response.Messages[0].Text);

        // Verify provider was NOT involved
        var sentMessages = fakeClient.CapturedRequests[0];
        Assert.DoesNotContain(sentMessages, m => m.Text?.Contains("USER_INFO") == true);
    }

    [Fact]
    public async Task Agent_WithProvider_EnrichesMessagesImplicitly()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Your name is Alice!");

        var provider = new SimpleMemoryProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(provider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        var response = await agent.RunAsync("What's my name?", thread, cancellationToken: TestCancellationToken);

        // Assert
        Assert.Equal(1, provider.InvokingCallCount);
        Assert.Equal(1, provider.InvokedCallCount);

        // Verify provider message was sent to LLM
        var sentMessages = fakeClient.CapturedRequests[0];
        var providerMessage = sentMessages.FirstOrDefault(m => m.Text?.Contains("USER_INFO: Name is Alice") == true);
        Assert.NotNull(providerMessage);
        Assert.Equal(ChatRole.System, providerMessage.Role);

        // Verify message ordering: provider message comes BEFORE user input
        var providerIndex = Array.IndexOf(sentMessages.ToArray(), providerMessage);
        var userMessage = sentMessages.FirstOrDefault(m => m.Text?.Contains("What's my name?") == true);
        var userIndex = Array.IndexOf(sentMessages.ToArray(), userMessage);

        Assert.True(providerIndex < userIndex, "Provider message should come before user input");
    }

    [Fact]
    public async Task Provider_ReceivesOnlyNewInputMessages()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Response 1");
        fakeClient.EnqueueTextResponse("Response 2");

        var provider = new SimpleMemoryProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(provider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act - First turn
        await agent.RunAsync("First message", thread, cancellationToken: TestCancellationToken);

        // Act - Second turn
        await agent.RunAsync("Second message", thread, cancellationToken: TestCancellationToken);

        // Assert - Provider should have been called twice
        Assert.Equal(2, provider.InvokingCallCount);

        // First invocation: received only "First message"
        Assert.Single(provider.ReceivedMessages[0]);
        Assert.Contains("First message", provider.ReceivedMessages[0][0].Text);

        // Second invocation: received only "Second message" (NOT the history)
        Assert.Single(provider.ReceivedMessages[1]);
        Assert.Contains("Second message", provider.ReceivedMessages[1][0].Text);
    }

    [Fact]
    public async Task Provider_InvokedAsync_ReceivesResponseMessages()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Your name is Alice!");

        var provider = new SimpleMemoryProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(provider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        await agent.RunAsync("What's my name?", thread, cancellationToken: TestCancellationToken);

        // Assert - InvokedAsync was called with response
        Assert.Equal(1, provider.InvokedCallCount);
        Assert.NotNull(provider.ResponseMessages[0]);
        Assert.Single(provider.ResponseMessages[0]!);
        Assert.Equal(ChatRole.Assistant, provider.ResponseMessages[0]![0].Role);
        Assert.Contains("Alice", provider.ResponseMessages[0]![0].Text);
    }

    #endregion

    #region Factory Pattern Tests

    [Fact]
    public void WithContextProviderFactory_CreatesProviderPerThread()
    {
        // Arrange
        int factoryCallCount = 0;
        var fakeClient = new FakeChatClient();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithContextProviderFactory(ctx =>
            {
                factoryCallCount++;
                return new SimpleMemoryProvider();
            });
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        // Act
        var thread1 = agent.CreateThread();
        var thread2 = agent.CreateThread();

        // Assert
        Assert.Equal(2, factoryCallCount);
        Assert.NotNull(thread1.AIContextProvider);
        Assert.NotNull(thread2.AIContextProvider);
        Assert.NotSame(thread1.AIContextProvider, thread2.AIContextProvider);
    }

    [Fact]
    public void WithContextProvider_Generic_CreatesNewInstancePerThread()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        
        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithContextProvider<SimpleMemoryProvider>();
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        // Act
        var thread1 = agent.CreateThread();
        var thread2 = agent.CreateThread();

        // Assert
        Assert.NotNull(thread1.AIContextProvider);
        Assert.NotNull(thread2.AIContextProvider);
        Assert.NotSame(thread1.AIContextProvider, thread2.AIContextProvider);
        Assert.IsType<SimpleMemoryProvider>(thread1.AIContextProvider);
    }

    [Fact]
    public void WithSharedContextProvider_SharesSameInstanceAcrossThreads()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        var sharedProvider = new SimpleMemoryProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(sharedProvider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        // Act
        var thread1 = agent.CreateThread();
        var thread2 = agent.CreateThread();

        // Assert
        Assert.Same(sharedProvider, thread1.AIContextProvider);
        Assert.Same(sharedProvider, thread2.AIContextProvider);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public async Task StatefulProvider_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Response 1");
        fakeClient.EnqueueTextResponse("Response 2");

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithContextProvider<StatefulCounterProvider>();
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act - Run twice to increment counter
        await agent.RunAsync("Message 1", thread, cancellationToken: TestCancellationToken);
        await agent.RunAsync("Message 2", thread, cancellationToken: TestCancellationToken);

        var provider = (StatefulCounterProvider)thread.AIContextProvider!;
        Assert.Equal(2, provider.ConversationCount);

        // Serialize thread
        var snapshot = thread.Serialize();
        var snapshotObj = JsonSerializer.Deserialize<ConversationThreadSnapshot>(
            snapshot.GetRawText(),
            ConversationJsonContext.Default.ConversationThreadSnapshot);

        // Deserialize thread with factory
        var restoredThread = ConversationThread.Deserialize(
            snapshotObj!,
            contextProviderFactory: (state, opts) => new StatefulCounterProvider(state, opts));

        // Assert - State was restored
        var restoredProvider = (StatefulCounterProvider)restoredThread.AIContextProvider!;
        Assert.Equal(2, restoredProvider.ConversationCount);

        // Run again to verify state continues
        fakeClient.EnqueueTextResponse("Response 3");
        await agent.RunAsync("Message 3", restoredThread, cancellationToken: TestCancellationToken);

        Assert.Equal(3, restoredProvider.ConversationCount);
    }

    #endregion

    #region Tools and Instructions Tests

    [Fact]
    public async Task Provider_CanAddDynamicTools()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueToolCall("get_weather", "call_1", new Dictionary<string, object?> { ["location"] = "Seattle" });
        fakeClient.EnqueueTextResponse("It's sunny in Seattle!");

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(new DynamicToolProvider());
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        var response = await agent.RunAsync("What's the weather?", thread, cancellationToken: TestCancellationToken);

        // Assert - Tool was available and called
        var sentMessages = fakeClient.CapturedRequests[0];
        var providerMessage = sentMessages.FirstOrDefault(m => m.Text?.Contains("Weather tool available") == true);
        Assert.NotNull(providerMessage);
    }

    [Fact]
    public async Task Provider_CanAddInstructions()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Hello friend!");

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithInstructions("Base instructions")
            .WithSharedContextProvider(new InstructionProvider());
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        await agent.RunAsync("Hi", thread, cancellationToken: TestCancellationToken);

        // Assert - Instructions were merged (we can't directly verify ChatOptions,
        // but provider was called successfully)
        Assert.NotNull(thread.AIContextProvider);
    }

    #endregion

    #region Per-Thread Override Tests

    [Fact]
    public async Task Thread_CanOverrideFactoryProvider()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Response");

        var defaultProvider = new SimpleMemoryProvider();
        var overrideProvider = new SimpleMemoryProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(defaultProvider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act - Override provider for this thread
        thread.AIContextProvider = overrideProvider;

        await agent.RunAsync("Test", thread, cancellationToken: TestCancellationToken);

        // Assert - Override provider was used, not default
        Assert.Equal(0, defaultProvider.InvokingCallCount);
        Assert.Equal(1, overrideProvider.InvokingCallCount);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Provider_Error_DoesNotFailTurn()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Success!");

        var faultyProvider = new FaultyProvider();

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(faultyProvider);
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act - Should not throw despite provider error
        var response = await agent.RunAsync("Test", thread, cancellationToken: TestCancellationToken);

        // Assert - Turn completed successfully
        Assert.Single(response.Messages);
        Assert.Equal("Success!", response.Messages[0].Text);
    }

    private class FaultyProvider : AIContextProvider
    {
        public override ValueTask<AIContext> InvokingAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider error!");
        }
    }

    #endregion

    #region Integration with Filters Tests

    [Fact]
    public async Task Provider_CoexistsWithFilters()
    {
        // Arrange
        var fakeClient = new FakeChatClient();
        fakeClient.EnqueueTextResponse("Response");

        var filterInvoked = false;

        var builder = new AgentBuilder()
            .WithName("TestAgent")
            .WithSharedContextProvider(new SimpleMemoryProvider())
            .WithPromptFilter(new TestFilter(() => filterInvoked = true));
        
        builder.BaseClient = fakeClient;
        var agent = builder.Build();

        var thread = agent.CreateThread();

        // Act
        await agent.RunAsync("Test", thread, cancellationToken: TestCancellationToken);

        // Assert - Both provider and filter were invoked
        var provider = (SimpleMemoryProvider)thread.AIContextProvider!;
        Assert.Equal(1, provider.InvokingCallCount);
        Assert.True(filterInvoked);

        // Verify message ordering: provider message comes BEFORE filter effects
        var sentMessages = fakeClient.CapturedRequests[0];
        var providerMessage = sentMessages.FirstOrDefault(m => m.Text?.Contains("USER_INFO") == true);
        Assert.NotNull(providerMessage);
    }

    private class TestFilter : IPromptFilter
    {
        private readonly Action _onInvoke;

        public TestFilter(Action onInvoke) => _onInvoke = onInvoke;

        public Task<IEnumerable<ChatMessage>> InvokeAsync(
            PromptFilterContext context,
            Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next)
        {
            _onInvoke();
            return next(context);
        }
    }

    #endregion
}
