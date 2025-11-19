using Microsoft.Extensions.AI;
using Xunit;

namespace HPD.Agent.Tests.Core;

/// <summary>
/// Unit tests for synchronous message API on ConversationThread.
/// Tests the new public sync methods: Messages, MessageCount, AddMessage(), AddMessages().
/// </summary>
public class SyncMessageAPITests
{
    [Fact]
    public void Messages_Property_Returns_InMemory_Messages_Directly()
    {
        // Arrange
        var thread = new ConversationThread();
        var msg = new ChatMessage(ChatRole.User, "Test");
        thread.AddMessage(msg);

        // Act
        var messages = thread.Messages;

        // Assert
        Assert.Single(messages);
        Assert.Equal("Test", messages[0].Text);
    }

    [Fact]
    public void Messages_Property_Returns_Snapshot_Not_LiveView()
    {
        // Arrange
        var thread = new ConversationThread();
        thread.AddMessage(new ChatMessage(ChatRole.User, "Message 1"));

        // Act - capture first snapshot
        var snapshot1 = thread.Messages;

        // Add another message
        thread.AddMessage(new ChatMessage(ChatRole.User, "Message 2"));

        // Capture second snapshot
        var snapshot2 = thread.Messages;

        // Assert - snapshots are different
        Assert.Single(snapshot1);
        Assert.Equal(2, snapshot2.Count);

        // Verify first snapshot didn't mutate (true snapshot behavior)
        Assert.Single(snapshot1);
    }

    [Fact]
    public void MessageCount_Returns_Correct_Count()
    {
        // Arrange
        var thread = new ConversationThread();

        // Act & Assert
        Assert.Equal(0, thread.MessageCount);

        thread.AddMessage(new ChatMessage(ChatRole.User, "Test 1"));
        Assert.Equal(1, thread.MessageCount);

        thread.AddMessage(new ChatMessage(ChatRole.User, "Test 2"));
        Assert.Equal(2, thread.MessageCount);
    }

    [Fact]
    public void AddMessage_Adds_To_InMemory_Store()
    {
        // Arrange
        var thread = new ConversationThread();
        var msg = new ChatMessage(ChatRole.User, "Test");

        // Act
        thread.AddMessage(msg);

        // Assert
        Assert.Single(thread.Messages);
        Assert.Equal("Test", thread.Messages[0].Text);
    }

    [Fact]
    public void AddMessages_Adds_Multiple_To_Store()
    {
        // Arrange
        var thread = new ConversationThread();
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Test 1"),
            new ChatMessage(ChatRole.User, "Test 2"),
            new ChatMessage(ChatRole.User, "Test 3")
        };

        // Act
        thread.AddMessages(messages);

        // Assert
        Assert.Equal(3, thread.MessageCount);
        Assert.Equal("Test 1", thread.Messages[0].Text);
        Assert.Equal("Test 3", thread.Messages[2].Text);
    }

    [Fact]
    public void RequiresAsyncAccess_Returns_False_For_InMemory()
    {
        // Arrange
        var thread = new ConversationThread();

        // Act & Assert
        Assert.False(thread.RequiresAsyncAccess);
    }

    [Fact]
    public async Task Sync_And_Async_APIs_Return_Same_Results()
    {
        // Arrange
        var thread = new ConversationThread();
        var msg1 = new ChatMessage(ChatRole.User, "Test 1");
        var msg2 = new ChatMessage(ChatRole.User, "Test 2");

        // Act - use both sync and async APIs
        thread.AddMessage(msg1);  // Sync
        await thread.AddMessageAsync(msg2);  // Async

        var syncMessages = thread.Messages;  // Sync read
        var syncCount = thread.MessageCount;  // Sync count
        var asyncCount = await thread.GetMessageCountAsync();  // Async count

        // Assert
        Assert.Equal(2, syncMessages.Count);
        Assert.Equal(2, syncCount);
        Assert.Equal(2, asyncCount);
        Assert.Equal("Test 1", syncMessages[0].Text);
        Assert.Equal("Test 2", syncMessages[1].Text);
    }

    [Fact]
    public void AddMessage_Updates_LastActivity()
    {
        // Arrange
        var thread = new ConversationThread();
        var initialActivity = thread.LastActivity;

        // Small delay to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        thread.AddMessage(new ChatMessage(ChatRole.User, "Test"));

        // Assert
        Assert.True(thread.LastActivity > initialActivity);
    }

    [Fact]
    public void AddMessages_Updates_LastActivity()
    {
        // Arrange
        var thread = new ConversationThread();
        var initialActivity = thread.LastActivity;

        // Small delay to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        thread.AddMessages(new[]
        {
            new ChatMessage(ChatRole.User, "Test 1"),
            new ChatMessage(ChatRole.User, "Test 2")
        });

        // Assert
        Assert.True(thread.LastActivity > initialActivity);
    }

    [Fact]
    public void Messages_Property_Is_ReadOnly()
    {
        // Arrange
        var thread = new ConversationThread();
        thread.AddMessage(new ChatMessage(ChatRole.User, "Test"));

        // Act
        var messages = thread.Messages;

        // Assert - should be IReadOnlyList, not IList
        Assert.IsAssignableFrom<IReadOnlyList<ChatMessage>>(messages);
    }

    [Fact]
    public void AddMessages_With_Empty_Collection_Does_Not_Throw()
    {
        // Arrange
        var thread = new ConversationThread();

        // Act & Assert - should not throw
        thread.AddMessages(Array.Empty<ChatMessage>());

        Assert.Equal(0, thread.MessageCount);
    }

    [Fact]
    public void Multiple_Calls_To_Messages_Property_Return_Different_Snapshots()
    {
        // Arrange
        var thread = new ConversationThread();

        // Act
        var snapshot1 = thread.Messages;
        thread.AddMessage(new ChatMessage(ChatRole.User, "Test"));
        var snapshot2 = thread.Messages;

        // Assert - different references (different snapshots)
        Assert.NotSame(snapshot1, snapshot2);
        Assert.Empty(snapshot1);
        Assert.Single(snapshot2);
    }
}
