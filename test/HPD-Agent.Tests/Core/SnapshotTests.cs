using Microsoft.Extensions.AI;
using Xunit;
using HPD.Agent;
using HPD.Agent.Checkpointing;
using HPD.Agent.Checkpointing.Services;
using HPD.Agent.Tests.Infrastructure;
using System.Text.Json;

namespace HPD.Agent.Tests.Core;

/// <summary>
/// Comprehensive tests for lightweight snapshot functionality.
/// Tests serialization, storage, size validation, and branching integration.
/// </summary>
public class SnapshotTests : AgentTestBase
{
    //──────────────────────────────────────────────────────────────────
    // SNAPSHOT SERIALIZATION TESTS
    //──────────────────────────────────────────────────────────────────

    [Fact]
    public void ThreadSnapshot_Serialize_ProducesValidJson()
    {
        // Arrange: Create a thread with messages
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Hello"));
        thread.AddMessage(AssistantMessage("Hi there!"));

        // Act: Convert to snapshot
        var snapshot = thread.ToSnapshot();

        // Assert: Snapshot should contain conversation data
        Assert.Equal(thread.Id, snapshot.ThreadId);
        Assert.Equal(2, snapshot.Messages.Count);
        Assert.Equal(thread.CreatedAt, snapshot.CreatedAt);
        Assert.Null(snapshot.MiddlewarePersistentState); // Empty by default
    }

    [Fact]
    public void ThreadSnapshot_Roundtrip_PreservesData()
    {
        // Arrange: Create thread with full state
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test message"));
        thread.DisplayName = "Test Thread";
        thread.ServiceThreadId = "service-123";
        thread.ConversationId = "conv-456";
        thread.CurrentCheckpointId = "checkpoint-789";
        thread.ActiveBranch = "main";
        thread.TryAddBranch("main", "checkpoint-789");
        thread.TryAddBranch("feature", "checkpoint-abc");

        // Add middleware persistent state
        thread.SetMiddlewarePersistentState("test-key", "test-value");

        // Act: Snapshot -> Serialize -> Deserialize -> Thread
        var snapshot = thread.ToSnapshot();
        var json = JsonSerializer.Serialize(snapshot, HPDJsonContext.Default.ThreadSnapshot);
        var restored = JsonSerializer.Deserialize(json, HPDJsonContext.Default.ThreadSnapshot);
        var restoredThread = ConversationThread.FromSnapshot(restored!);

        // Assert: All conversation-level data preserved
        Assert.Equal(thread.Id, restoredThread.Id);
        Assert.Equal(thread.MessageCount, restoredThread.MessageCount);
        Assert.Equal(thread.DisplayName, restoredThread.DisplayName);
        Assert.Equal(thread.ServiceThreadId, restoredThread.ServiceThreadId);
        Assert.Equal(thread.ConversationId, restoredThread.ConversationId);
        Assert.Equal(thread.CurrentCheckpointId, restoredThread.CurrentCheckpointId);
        Assert.Equal(thread.ActiveBranch, restoredThread.ActiveBranch);
        Assert.Equal(2, restoredThread.Branches.Count);
        Assert.Equal("test-value", restoredThread.GetMiddlewarePersistentState("test-key"));

        // Assert: ExecutionState NOT preserved (snapshot doesn't include it)
        Assert.Null(restoredThread.ExecutionState);
    }

    [Fact]
    public void ThreadSnapshot_ExcludesExecutionState()
    {
        // Arrange: Create thread with execution state
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));
        var messages = new List<ChatMessage> { UserMessage("Test") };
        thread.ExecutionState = AgentLoopState.Initial(messages, "run-1", "conv-1", "TestAgent");

        // Act: Convert to snapshot and serialize
        var snapshot = thread.ToSnapshot();
        var json = JsonSerializer.Serialize(snapshot, HPDJsonContext.Default.ThreadSnapshot);

        // Assert: JSON should NOT contain execution state
        Assert.DoesNotContain("ExecutionState", json);
        Assert.DoesNotContain("AgentLoopState", json);
        Assert.DoesNotContain("ResponseUpdates", json);
        Assert.DoesNotContain("PendingWrites", json);
    }

    [Fact]
    public void ExecutionCheckpoint_Serialize_IncludesExecutionState()
    {
        // Arrange: Create thread with execution state
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));
        var messages = new List<ChatMessage> { UserMessage("Test") };
        thread.ExecutionState = AgentLoopState.Initial(messages, "run-1", "conv-1", "TestAgent");

        // Act: Convert to execution checkpoint
        var checkpoint = thread.ToExecutionCheckpoint();

        // Assert: Checkpoint includes execution state
        Assert.NotNull(checkpoint.ExecutionState);
        Assert.Equal("run-1", checkpoint.ExecutionState.RunId);
        Assert.Equal("conv-1", checkpoint.ExecutionState.ConversationId);
    }

    [Fact]
    public void ExecutionCheckpoint_ThrowsIfNoExecutionState()
    {
        // Arrange: Thread without execution state
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));

        // Act & Assert: Should throw
        var ex = Assert.Throws<InvalidOperationException>(() => thread.ToExecutionCheckpoint());
        Assert.Contains("ExecutionState", ex.Message);
    }

    [Fact]
    public void ExecutionCheckpoint_Roundtrip_PreservesAllData()
    {
        // Arrange: Create full thread with execution state
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test message"));
        thread.DisplayName = "Test Thread";
        var messages = new List<ChatMessage> { UserMessage("Test message") };
        thread.ExecutionState = AgentLoopState.Initial(messages, "run-1", "conv-1", "TestAgent")
            .NextIteration();

        // Act: Checkpoint -> Serialize -> Deserialize -> Thread
        var checkpoint = thread.ToExecutionCheckpoint();
        var json = JsonSerializer.Serialize(checkpoint, HPDJsonContext.Default.ExecutionCheckpoint);
        var restored = JsonSerializer.Deserialize(json, HPDJsonContext.Default.ExecutionCheckpoint);
        var restoredThread = ConversationThread.FromExecutionCheckpoint(restored!);

        // Assert: All data preserved including execution state
        Assert.Equal(thread.Id, restoredThread.Id);
        Assert.NotNull(restoredThread.ExecutionState);
        Assert.Equal(1, restoredThread.ExecutionState.Iteration);
        Assert.Equal("run-1", restoredThread.ExecutionState.RunId);
    }

    //──────────────────────────────────────────────────────────────────
    // SNAPSHOT STORAGE TESTS (JsonConversationThreadStore)
    //──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JsonStore_SaveSnapshot_CreatesSnapshotFile()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));

        var snapshot = thread.ToSnapshot();
        var metadata = new CheckpointMetadata
        {
            Source = CheckpointSource.Fork,
            Step = -1,
            MessageIndex = 1
        };

        // Act
        var snapshotId = await store.SaveSnapshotAsync(thread.Id, snapshot, metadata);

        // Assert: Snapshot file exists with .snapshot.json extension
        var snapshotPath = Path.Combine(tempDir.Path, "threads", thread.Id, $"{snapshotId}.snapshot.json");
        Assert.True(File.Exists(snapshotPath));

        // Assert: Manifest updated with IsSnapshot=true
        var manifest = await store.GetCheckpointManifestAsync(thread.Id);
        Assert.Single(manifest);
        Assert.True(manifest[0].IsSnapshot);
        Assert.Equal(snapshotId, manifest[0].CheckpointId);
    }

    [Fact]
    public async Task JsonStore_LoadSnapshot_RestoresSnapshot()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test message"));
        thread.DisplayName = "Original";

        var snapshot = thread.ToSnapshot();
        var metadata = new CheckpointMetadata
        {
            Source = CheckpointSource.Fork,
            Step = -1,
            MessageIndex = 1
        };

        var snapshotId = await store.SaveSnapshotAsync(thread.Id, snapshot, metadata);

        // Act
        var loaded = await store.LoadSnapshotAsync(thread.Id, snapshotId);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(thread.Id, loaded.ThreadId);
        Assert.Single(loaded.Messages);
        Assert.Equal("Original", loaded.Metadata["DisplayName"]?.ToString());
    }

    [Fact]
    public async Task JsonStore_LoadSnapshot_ReturnsNullIfNotFound()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);

        // Act
        var loaded = await store.LoadSnapshotAsync("nonexistent", "snapshot-1");

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task JsonStore_DeleteSnapshots_RemovesFiles()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));

        var snapshot = thread.ToSnapshot();
        var metadata = new CheckpointMetadata { Source = CheckpointSource.Fork, Step = -1, MessageIndex = 1 };

        var snapshotId1 = await store.SaveSnapshotAsync(thread.Id, snapshot, metadata);
        var snapshotId2 = await store.SaveSnapshotAsync(thread.Id, snapshot, metadata);

        // Act
        await store.DeleteSnapshotsAsync(thread.Id, new[] { snapshotId1 });

        // Assert: snapshot-1 deleted, snapshot-2 remains
        var loaded1 = await store.LoadSnapshotAsync(thread.Id, snapshotId1);
        var loaded2 = await store.LoadSnapshotAsync(thread.Id, snapshotId2);
        Assert.Null(loaded1);
        Assert.NotNull(loaded2);

        var manifest = await store.GetCheckpointManifestAsync(thread.Id);
        Assert.Single(manifest);
        Assert.Equal(snapshotId2, manifest[0].CheckpointId);
    }

    [Fact]
    public async Task JsonStore_PruneSnapshots_KeepsLatest()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));

        var snapshot = thread.ToSnapshot();
        var metadata = new CheckpointMetadata { Source = CheckpointSource.Fork, Step = -1, MessageIndex = 1 };

        // Create 5 snapshots
        var snapshotIds = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            var id = await store.SaveSnapshotAsync(thread.Id, snapshot, metadata);
            snapshotIds.Add(id);
            await Task.Delay(10); // Ensure different timestamps
        }

        // Act: Keep only 3 latest
        await store.PruneSnapshotsAsync(thread.Id, keepLatest: 3);

        // Assert: Only 3 most recent remain
        var manifest = await store.GetCheckpointManifestAsync(thread.Id);
        Assert.Equal(3, manifest.Count(m => m.IsSnapshot));

        var loaded1 = await store.LoadSnapshotAsync(thread.Id, snapshotIds[0]);
        var loaded5 = await store.LoadSnapshotAsync(thread.Id, snapshotIds[4]);
        Assert.Null(loaded1); // Old snapshot pruned
        Assert.NotNull(loaded5); // Recent snapshot kept
    }

    //──────────────────────────────────────────────────────────────────
    // SIZE VALIDATION TESTS
    //──────────────────────────────────────────────────────────────────

    [Fact]
    public void ThreadSnapshot_Size_IsSignificantlySmallerThanCheckpoint()
    {
        // Arrange: Create thread with moderate conversation
        var thread = new ConversationThread();
        for (int i = 0; i < 10; i++)
        {
            thread.AddMessage(UserMessage($"User message {i}"));
            thread.AddMessage(AssistantMessage($"Assistant response {i}"));
        }

        // Add execution state (heavyweight)
        var messages = thread.Messages.ToList();
        thread.ExecutionState = AgentLoopState.Initial(messages, "run-1", "conv-1", "TestAgent")
            .NextIteration()
            .NextIteration();

        // Act: Serialize both formats
        var snapshot = thread.ToSnapshot();
        var checkpoint = thread.ToExecutionCheckpoint();

        var snapshotJson = JsonSerializer.Serialize(snapshot, HPDJsonContext.Default.ThreadSnapshot);
        var checkpointJson = JsonSerializer.Serialize(checkpoint, HPDJsonContext.Default.ExecutionCheckpoint);

        // Assert: Snapshot should be significantly smaller
        var snapshotSize = snapshotJson.Length;
        var checkpointSize = checkpointJson.Length;

        Assert.True(snapshotSize < checkpointSize, 
            $"Snapshot ({snapshotSize} bytes) should be smaller than checkpoint ({checkpointSize} bytes)");

        // Ratio should be at least 2x smaller (typically 6x with real execution state)
        var ratio = (double)checkpointSize / snapshotSize;
        Assert.True(ratio >= 2.0, 
            $"Checkpoint should be at least 2x larger than snapshot (actual: {ratio:F2}x)");
    }

    [Fact]
    public void ThreadSnapshot_TypicalSize_IsUnder50KB()
    {
        // Arrange: Create thread with realistic conversation (50 messages)
        var thread = new ConversationThread();
        for (int i = 0; i < 25; i++)
        {
            thread.AddMessage(UserMessage($"This is user message number {i} with some reasonable content that simulates a real conversation."));
            thread.AddMessage(AssistantMessage($"This is assistant response number {i} with detailed information and helpful content for the user's question."));
        }

        // Add middleware persistent state
        thread.SetMiddlewarePersistentState("HPD.Agent.HistoryReductionStateData", "{\"lastReduction\":\"...\"}");

        // Act: Serialize snapshot
        var snapshot = thread.ToSnapshot();
        var json = JsonSerializer.Serialize(snapshot, HPDJsonContext.Default.ThreadSnapshot);

        // Assert: Should be under 50KB for typical usage
        var sizeKB = json.Length / 1024.0;
        Assert.True(sizeKB < 50, $"Typical snapshot size ({sizeKB:F2} KB) should be under 50KB");
    }

    //──────────────────────────────────────────────────────────────────
    // MANIFEST INTEGRATION TESTS
    //──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Manifest_DistinguishesSnapshotsFromCheckpoints()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Test"));

        // Save a regular checkpoint (via SaveThreadAtCheckpointAsync)
        var checkpoint = thread.Serialize();
        await store.SaveThreadAtCheckpointAsync(
            thread, 
            "checkpoint-1", 
            new CheckpointMetadata { Source = CheckpointSource.Loop, Step = 0, MessageIndex = 1 });

        // Save a lightweight snapshot
        var snapshot = thread.ToSnapshot();
        var snapshotId = await store.SaveSnapshotAsync(
            thread.Id, 
            snapshot, 
            new CheckpointMetadata { Source = CheckpointSource.Fork, Step = -1, MessageIndex = 1 });

        // Act
        var manifest = await store.GetCheckpointManifestAsync(thread.Id);

        // Assert: Manifest has both entries with correct IsSnapshot flags
        Assert.Equal(2, manifest.Count);
        
        var snapshotEntry = manifest.First(e => e.CheckpointId == snapshotId);
        var checkpointEntry = manifest.First(e => e.CheckpointId == "checkpoint-1");

        Assert.True(snapshotEntry.IsSnapshot);
        Assert.False(checkpointEntry.IsSnapshot);
    }

    //──────────────────────────────────────────────────────────────────
    // BRANCHING INTEGRATION TESTS
    //──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BranchingService_Fork_UsesLightweightSnapshots()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var branchingService = new Branching(store, new BranchingConfig { Enabled = true });

        var thread = new ConversationThread();
        thread.AddMessage(UserMessage("Initial message"));

        // Save initial checkpoint
        await store.SaveThreadAsync(thread);
        
        // Save snapshot for branching (branching requires snapshots)
        var snapshotId = await store.SaveSnapshotAsync(
            thread.Id,
            thread.ToSnapshot(),
            new CheckpointMetadata
            {
                Source = CheckpointSource.Root,
                Step = -1,
                MessageIndex = thread.MessageCount,
                BranchName = "main"
            });

        // Act: Fork from the snapshot
        var (forkedThread, evt) = await branchingService.ForkFromCheckpointAsync(
            thread.Id, 
            snapshotId, 
            "feature-branch");

        // Assert: Fork checkpoint should be saved as snapshot
        var updatedManifest = await store.GetCheckpointManifestAsync(thread.Id);
        var forkEntry = updatedManifest.FirstOrDefault(e => e.BranchName == "feature-branch");
        
        Assert.NotNull(forkEntry);
        Assert.True(forkEntry.IsSnapshot, "Fork should create a snapshot, not a full checkpoint");
        Assert.Equal(CheckpointSource.Fork, forkEntry.Source);

        // Assert: Snapshot file exists
        var snapshotPath = Path.Combine(tempDir.Path, "threads", thread.Id, $"{evt.CheckpointId}.snapshot.json");
        Assert.True(File.Exists(snapshotPath), "Snapshot file should exist with .snapshot.json extension");

        // Assert: Can load the snapshot
        var loadedSnapshot = await store.LoadSnapshotAsync(thread.Id, evt.CheckpointId);
        Assert.NotNull(loadedSnapshot);
        Assert.Single(loadedSnapshot.Messages);
    }

    [Fact]
    public async Task BranchingService_Fork_SnapshotSmallerThanCheckpoint()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var branchingService = new Branching(store, new BranchingConfig { Enabled = true });

        var thread = new ConversationThread();
        for (int i = 0; i < 10; i++)
        {
            thread.AddMessage(UserMessage($"Message {i}"));
        }

        // Add execution state (heavyweight)
        var messages = thread.Messages.ToList();
        thread.ExecutionState = AgentLoopState.Initial(messages, "run-1", "conv-1", "TestAgent");

        // Save checkpoint with execution state
        var checkpointId = Guid.NewGuid().ToString();
        await store.SaveThreadAtCheckpointAsync(
            thread, 
            checkpointId, 
            new CheckpointMetadata { Source = CheckpointSource.Loop, Step = 0, MessageIndex = 10 });

        var checkpointPath = Path.Combine(tempDir.Path, "threads", thread.Id, $"{checkpointId}.json");
        var checkpointSize = new FileInfo(checkpointPath).Length;

        // Save snapshot for branching (branching requires snapshots)
        var snapshotId = await store.SaveSnapshotAsync(
            thread.Id,
            thread.ToSnapshot(),
            new CheckpointMetadata { Source = CheckpointSource.Loop, Step = 0, MessageIndex = 10 });

        // Act: Fork (creates snapshot without execution state)
        var (forkedThread, evt) = await branchingService.ForkFromCheckpointAsync(
            thread.Id, 
            snapshotId, 
            "feature-branch");

        // Assert: Snapshot is significantly smaller
        var snapshotPath = Path.Combine(tempDir.Path, "threads", thread.Id, $"{evt.CheckpointId}.snapshot.json");
        var snapshotSize = new FileInfo(snapshotPath).Length;

        Assert.True(snapshotSize < checkpointSize, 
            $"Snapshot ({snapshotSize} bytes) should be smaller than checkpoint ({checkpointSize} bytes)");

        var ratio = (double)checkpointSize / snapshotSize;
        Assert.True(ratio >= 1.5, 
            $"Checkpoint should be at least 1.5x larger (actual: {ratio:F2}x)");
    }

    [Fact]
    public async Task BranchingService_Copy_UsesLightweightSnapshots()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var store = new JsonConversationThreadStore(tempDir.Path);
        var branchingService = new Branching(store, new BranchingConfig { Enabled = true });

        var sourceThread = new ConversationThread();
        sourceThread.AddMessage(UserMessage("Source message"));
        sourceThread.DisplayName = "Source Thread";

        // Save initial checkpoint
        await store.SaveThreadAsync(sourceThread);

        // Save snapshot for branching (branching requires snapshots)
        var snapshotId = await store.SaveSnapshotAsync(
            sourceThread.Id,
            sourceThread.ToSnapshot(),
            new CheckpointMetadata { Source = CheckpointSource.Root, Step = -1, MessageIndex = sourceThread.MessageCount, BranchName = "main" });

        // Act: Copy to new thread
        var (copiedThread, evt) = await branchingService.CopyFromCheckpointAsync(
            sourceThread.Id, 
            snapshotId, 
            "Copied Thread");

        // Assert: Copy should create snapshot in new thread
        var copiedManifest = await store.GetCheckpointManifestAsync(copiedThread.Id);
        Assert.Single(copiedManifest);
        Assert.True(copiedManifest[0].IsSnapshot, "Copy should create a snapshot");
        Assert.Equal(CheckpointSource.Copy, copiedManifest[0].Source);

        // Assert: Snapshot file exists in new thread directory
        var snapshotPath = Path.Combine(tempDir.Path, "threads", copiedThread.Id, $"{evt.NewCheckpointId}.snapshot.json");
        Assert.True(File.Exists(snapshotPath));
    }

    //──────────────────────────────────────────────────────────────────
    // HELPER CLASS
    //──────────────────────────────────────────────────────────────────

    private class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hpd-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
