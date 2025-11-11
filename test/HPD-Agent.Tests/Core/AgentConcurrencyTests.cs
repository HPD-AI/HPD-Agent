using HPD.Agent;
using HPD.Agent.Conversation;
using HPD_Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Xunit;

namespace HPD_Agent.Tests.Core;

/// <summary>
/// Tests for Agent concurrency - verifies that one agent can serve multiple threads concurrently.
/// This validates the stateless architecture where Agent owns no conversation state.
/// </summary>
public class AgentConcurrencyTests : AgentTestBase
{
    /// <summary>
    /// Verifies that a single agent instance can handle two concurrent conversations
    /// on different threads without any state interference.
    /// </summary>
    [Fact]
    public async Task Agent_CanServeMultipleThreadsConcurrently()
    {
        // Arrange
        var chatClient = new FakeChatClient
        {
            CompleteAsyncCallback = async (messages, options, cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);  // Simulate some async work
                return new ChatCompletion(
                    new ChatMessage(ChatRole.Assistant, "Response"));
            }
        };

        var agent = CreateAgent(client: chatClient);

        var thread1 = agent.CreateThread();
        var thread2 = agent.CreateThread();

        var messages1 = new[] { new ChatMessage(ChatRole.User, "Hello from thread 1") };
        var messages2 = new[] { new ChatMessage(ChatRole.User, "Hello from thread 2") };

        // Act - Run both conversations concurrently on the same agent
        var (events1, events2) = await (
            agent.RunAsync(messages1, thread: thread1).ToListAsync().AsTask(),
            agent.RunAsync(messages2, thread: thread2).ToListAsync().AsTask()
        );

        // Assert - Both conversations completed successfully
        Assert.NotEmpty(events1);
        Assert.NotEmpty(events2);

        // Verify threads maintained separate state
        Assert.NotEqual(thread1.Id, thread2.Id);

        // Verify each thread has messages
        var messageCount1 = await thread1.GetMessageCountAsync();
        var messageCount2 = await thread2.GetMessageCountAsync();

        Assert.True(messageCount1 >= 2);  // User message + assistant response
        Assert.True(messageCount2 >= 2);
    }

    /// <summary>
    /// Verifies that ConversationId is maintained separately for each thread
    /// when the same agent serves multiple concurrent conversations.
    /// </summary>
    [Fact]
    public async Task Agent_MaintainsSeparateConversationIdsPerThread()
    {
        // Arrange
        var chatClient = new FakeChatClient
        {
            CompleteAsyncCallback = async (messages, options, cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);
                return new ChatCompletion(new ChatMessage(ChatRole.Assistant, "Response"));
            }
        };

        var agent = CreateAgent(client: chatClient);

        var thread1 = agent.CreateThread();
        var thread2 = agent.CreateThread();

        // Set different ConversationIds
        thread1.ConversationId = "conv-1";
        thread2.ConversationId = "conv-2";

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };

        // Act - Run both concurrently
        await Task.WhenAll(
            agent.RunAsync(messages, thread: thread1).ToListAsync().AsTask(),
            agent.RunAsync(messages, thread: thread2).ToListAsync().AsTask()
        );

        // Assert - Each thread retained its own ConversationId
        Assert.Equal("conv-1", thread1.ConversationId);
        Assert.Equal("conv-2", thread2.ConversationId);
    }

    /// <summary>
    /// Verifies that serialization/deserialization preserves ConversationId.
    /// </summary>
    [Fact]
    public void Thread_PreservesConversationIdAfterSerialization()
    {
        // Arrange
        var thread = new ConversationThread();
        thread.ConversationId = "test-conv-id";

        // Act - Serialize and deserialize
        var snapshot = thread.Serialize();
        var restoredThread = ConversationThread.Deserialize(snapshot);

        // Assert - ConversationId preserved
        Assert.Equal("test-conv-id", restoredThread.ConversationId);
    }
}
