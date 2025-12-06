using Microsoft.Extensions.AI;

namespace HPD.Agent.Checkpointing.Services;

/// <summary>
/// Service for branching operations (fork, copy, switch, delete, rename).
/// Provides Git-like branching semantics for conversation threads.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Design Decision: Fork vs Copy Semantics</strong>
/// </para>
/// <para>
/// Two distinct operations are supported:
/// <list type="bullet">
/// <item><b>Fork</b>: Creates a new branch within the SAME thread (Git-like branches)</item>
/// <item><b>Copy</b>: Creates a NEW independent thread with lineage tracking</item>
/// </list>
/// </para>
/// <para>
/// Fork is preferred for "edit message and retry" UX (same conversation, different paths).
/// Copy is for "start fresh from this point" UX (new conversation based on history).
/// </para>
/// </remarks>
public class Branching
{
    private readonly ICheckpointStore _store;
    private readonly BranchingConfig _config;
    private readonly List<ICheckpointObserver> _observers = new();

    /// <summary>
    /// Creates a new BranchingService.
    /// </summary>
    /// <param name="store">The checkpoint store (shared with DurableExecutionService)</param>
    /// <param name="config">Configuration for branching</param>
    public Branching(ICheckpointStore store, BranchingConfig config)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets whether branching is enabled.
    /// </summary>
    public bool IsEnabled => _config.Enabled;

    /// <summary>
    /// Register an observer to be notified of branching events.
    /// </summary>
    public void RegisterObserver(ICheckpointObserver observer)
    {
        _observers.Add(observer);
    }

    /// <summary>
    /// Unregister an observer.
    /// </summary>
    public void UnregisterObserver(ICheckpointObserver observer)
    {
        _observers.Remove(observer);
    }

    //──────────────────────────────────────────────────────────────────
    // FORK OPERATIONS
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fork from a checkpoint, creating a new BRANCH within the SAME thread.
    /// This is Git-like behavior: same repo, different branches.
    /// </summary>
    /// <param name="threadId">Thread to fork from</param>
    /// <param name="checkpointId">Checkpoint to fork from</param>
    /// <param name="newBranchName">Optional name for the new branch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Thread state at the fork point and the branch created event</returns>
    /// <exception cref="InvalidOperationException">If branching is not enabled</exception>
    /// <exception cref="ArgumentException">If checkpoint not found</exception>
    public async Task<(ConversationThread Thread, BranchCreatedEvent Event)> ForkFromCheckpointAsync(
        string threadId,
        string checkpointId,
        string? newBranchName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);

        // Load the source checkpoint (state at the fork point)
        var sourceThread = await _store.LoadThreadAtCheckpointAsync(threadId, checkpointId, cancellationToken);
        if (sourceThread == null)
            throw new ArgumentException($"Checkpoint {checkpointId} not found in thread {threadId}");

        // Load the CURRENT thread state to get up-to-date branch info and the latest checkpoint
        var currentThread = await _store.LoadThreadAsync(threadId, cancellationToken);
        var previousBranch = currentThread?.ActiveBranch;
        var currentCheckpointId = currentThread?.CurrentCheckpointId;

        // Get the manifest to find the latest checkpoint if currentCheckpointId is not set
        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);
        var sourceEntry = manifest.FirstOrDefault(c => c.CheckpointId == checkpointId);
        var forkMessageIndex = sourceEntry?.MessageIndex ?? sourceThread.MessageCount;

        // If we don't have a currentCheckpointId, find the latest from manifest
        if (string.IsNullOrWhiteSpace(currentCheckpointId) && manifest.Count > 0)
        {
            // Get the most recent non-root checkpoint (manifest is ordered newest first)
            var latestEntry = manifest.FirstOrDefault(c => c.Source != CheckpointSource.Root);
            currentCheckpointId = latestEntry?.CheckpointId ?? manifest[0].CheckpointId;
        }

        // Generate branch name if not provided
        var branchName = newBranchName ?? $"branch-{Guid.NewGuid().ToString()[..8]}";

        // Create new checkpoint for the fork
        var forkCheckpointId = Guid.NewGuid().ToString();

        // Copy current branch data to the forked thread
        if (currentThread != null)
        {
            foreach (var branch in currentThread.Branches)
            {
                sourceThread.TryAddBranch(branch.Key, branch.Value);
            }
        }

        // CRITICAL: Preserve the original conversation path before switching to the new branch
        // If there was no active branch (anonymous/detached state), create a "main" branch
        // pointing to the latest checkpoint so the original conversation is not lost
        if (string.IsNullOrWhiteSpace(previousBranch))
        {
            // No branch was active - create "main" to preserve the original path
            if (!string.IsNullOrWhiteSpace(currentCheckpointId))
            {
                sourceThread.TryAddBranch("main", currentCheckpointId);
            }
        }
        else if (!string.IsNullOrWhiteSpace(currentCheckpointId))
        {
            // Update the previous branch's head to point to the current (latest) checkpoint
            sourceThread.TryUpdateBranch(previousBranch, currentCheckpointId);
        }

        // Update the thread with fork state
        sourceThread.CurrentCheckpointId = forkCheckpointId;
        sourceThread.ActiveBranch = branchName;
        sourceThread.TryAddBranch(branchName, forkCheckpointId);

        // Create metadata for the fork checkpoint
        var metadata = new CheckpointMetadata
        {
            Source = CheckpointSource.Fork,
            Step = sourceThread.ExecutionState?.Metadata?.Step ?? -1,
            ParentCheckpointId = checkpointId,
            BranchName = branchName,
            MessageIndex = forkMessageIndex
        };

        // Save the fork checkpoint
        await _store.SaveThreadAtCheckpointAsync(sourceThread, forkCheckpointId, metadata, cancellationToken);

        // Create the event
        var evt = new BranchCreatedEvent(
            threadId,
            branchName,
            forkCheckpointId,
            checkpointId,
            forkMessageIndex);

        // Notify observers
        NotifyObservers(o => o.OnBranchCreated(evt));

        return (sourceThread, evt);
    }

    //──────────────────────────────────────────────────────────────────
    // COPY OPERATIONS (Cross-Thread)
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Copy from a checkpoint, creating a NEW INDEPENDENT thread.
    /// Unlike Fork (which creates branches in the same thread), Copy creates
    /// a completely separate thread with lineage tracking back to the source.
    /// </summary>
    /// <param name="sourceThreadId">Thread to copy from</param>
    /// <param name="sourceCheckpointId">Checkpoint to copy from</param>
    /// <param name="newDisplayName">Optional display name for the new thread</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New independent thread and the copy event</returns>
    /// <exception cref="InvalidOperationException">If branching is not enabled</exception>
    /// <exception cref="ArgumentException">If source checkpoint not found</exception>
    public async Task<(ConversationThread Thread, ThreadCopiedEvent Event)> CopyFromCheckpointAsync(
        string sourceThreadId,
        string sourceCheckpointId,
        string? newDisplayName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceThreadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCheckpointId);

        // Load the source checkpoint
        var sourceThread = await _store.LoadThreadAtCheckpointAsync(
            sourceThreadId, sourceCheckpointId, cancellationToken);

        if (sourceThread == null)
            throw new ArgumentException($"Checkpoint {sourceCheckpointId} not found in thread {sourceThreadId}");

        // Create a new independent thread
        var newThread = new ConversationThread();

        // Copy display name or generate one
        if (!string.IsNullOrWhiteSpace(newDisplayName))
        {
            newThread.DisplayName = newDisplayName;
        }
        else
        {
            var sourceDisplayName = sourceThread.GetDisplayName();
            newThread.DisplayName = $"Copy of {sourceDisplayName}";
        }

        // Copy messages from source
        if (sourceThread.Messages.Count > 0)
        {
            newThread.AddMessages(sourceThread.Messages);
        }

        // Copy execution state if present
        if (sourceThread.ExecutionState != null)
        {
            newThread.ExecutionState = sourceThread.ExecutionState;
        }

        // Create root checkpoint in the new thread with lineage
        var newCheckpointId = Guid.NewGuid().ToString();
        newThread.CurrentCheckpointId = newCheckpointId;
        newThread.TryAddBranch("main", newCheckpointId);
        newThread.ActiveBranch = "main";

        // Save the new thread with Copy metadata
        var metadata = new CheckpointMetadata
        {
            Source = CheckpointSource.Copy,
            Step = -1,
            ParentCheckpointId = sourceCheckpointId,
            ParentThreadId = sourceThreadId,
            BranchName = "main",
            MessageIndex = newThread.MessageCount
        };

        await _store.SaveThreadAtCheckpointAsync(newThread, newCheckpointId, metadata, cancellationToken);

        // Create the event
        var messageIndex = sourceThread.MessageCount - 1;
        var evt = new ThreadCopiedEvent(
            sourceThreadId,
            newThread.Id,
            sourceCheckpointId,
            newCheckpointId,
            messageIndex);

        // Notify observers
        NotifyObservers(o => o.OnThreadCopied(evt));

        return (newThread, evt);
    }

    //──────────────────────────────────────────────────────────────────
    // BRANCH NAVIGATION
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switch to a different branch.
    /// </summary>
    /// <param name="threadId">Thread to switch branches on</param>
    /// <param name="branchName">Branch name to switch to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Thread at the branch head and the switch event, or null if branch not found</returns>
    public async Task<(ConversationThread Thread, BranchSwitchedEvent Event)?> SwitchBranchAsync(
        string threadId,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        // First, load the current thread to get the Branches dictionary
        // The branch -> checkpoint mapping is stored in the thread, not the manifest
        var currentThread = await _store.LoadThreadAsync(threadId, cancellationToken);
        
        string? targetCheckpointId = null;
        
        // Try to find the branch in the thread's Branches dictionary
        if (currentThread?.Branches.TryGetValue(branchName, out var branchCheckpointId) == true)
        {
            targetCheckpointId = branchCheckpointId;
        }
        else
        {
            // Fallback: check manifest for branches created via fork (they have BranchName set)
            var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);
            var branchHead = manifest.FirstOrDefault(c => c.BranchName == branchName);
            targetCheckpointId = branchHead?.CheckpointId;
        }

        if (targetCheckpointId == null)
            return null;

        // Load the checkpoint
        var thread = await _store.LoadThreadAtCheckpointAsync(threadId, targetCheckpointId, cancellationToken);
        if (thread == null)
            return null;

        var previousBranch = currentThread?.ActiveBranch ?? thread.ActiveBranch;

        // Copy over the current branch data to preserve all branches
        if (currentThread != null)
        {
            foreach (var branch in currentThread.Branches)
            {
                thread.TryAddBranch(branch.Key, branch.Value);
            }
        }

        // Update thread state
        thread.ActiveBranch = branchName;
        thread.CurrentCheckpointId = targetCheckpointId;

        // Create the event
        var evt = new BranchSwitchedEvent(
            threadId,
            previousBranch,
            branchName,
            targetCheckpointId);

        // Notify observers
        NotifyObservers(o => o.OnBranchSwitched(evt));

        return (thread, evt);
    }

    /// <summary>
    /// Get the full branch tree structure for visualization.
    /// </summary>
    public async Task<BranchTree> GetBranchTreeAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);

        if (manifest.Count == 0)
            throw new InvalidOperationException($"Thread {threadId} has no checkpoints");

        var nodes = new Dictionary<string, BranchNode>();
        var namedBranches = new Dictionary<string, BranchMetadata>();
        string? rootId = null;

        // Build parent -> children mapping
        var childrenMap = new Dictionary<string, List<string>>();
        foreach (var cp in manifest)
        {
            if (!childrenMap.ContainsKey(cp.CheckpointId))
                childrenMap[cp.CheckpointId] = new List<string>();

            if (cp.ParentCheckpointId != null)
            {
                if (!childrenMap.ContainsKey(cp.ParentCheckpointId))
                    childrenMap[cp.ParentCheckpointId] = new List<string>();
                childrenMap[cp.ParentCheckpointId].Add(cp.CheckpointId);
            }
            else
            {
                rootId = cp.CheckpointId;
            }
        }

        // Build nodes
        foreach (var cp in manifest)
        {
            nodes[cp.CheckpointId] = new BranchNode
            {
                CheckpointId = cp.CheckpointId,
                ParentCheckpointId = cp.ParentCheckpointId,
                ChildCheckpointIds = childrenMap.GetValueOrDefault(cp.CheckpointId, new List<string>()),
                MessageCount = cp.MessageIndex,
                CreatedAt = cp.CreatedAt,
                BranchName = cp.BranchName
            };

            // Collect named branches (use latest checkpoint for each branch name)
            if (cp.BranchName != null && !namedBranches.ContainsKey(cp.BranchName))
            {
                namedBranches[cp.BranchName] = new BranchMetadata
                {
                    Name = cp.BranchName,
                    HeadCheckpointId = cp.CheckpointId,
                    ForkMessageIndex = cp.MessageIndex,
                    CreatedAt = cp.CreatedAt,
                    LastActivity = cp.CreatedAt,
                    MessageCount = cp.MessageIndex
                };
            }
        }

        // Find root if not explicitly set (oldest checkpoint without parent)
        if (rootId == null)
        {
            var oldest = manifest.OrderBy(c => c.CreatedAt).FirstOrDefault();
            rootId = oldest?.CheckpointId ?? throw new InvalidOperationException("No root checkpoint found");
        }

        return new BranchTree
        {
            ThreadId = threadId,
            RootCheckpointId = rootId,
            Nodes = nodes,
            NamedBranches = namedBranches,
            ActiveBranch = null // Caller should set from thread state
        };
    }

    /// <summary>
    /// Get checkpoint history for a thread.
    /// </summary>
    public async Task<List<CheckpointTuple>> GetCheckpointsAsync(
        string threadId,
        int? limit = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        var manifest = await _store.GetCheckpointManifestAsync(threadId, limit, before, cancellationToken);

        // Load full checkpoint data for each entry
        var result = new List<CheckpointTuple>();
        foreach (var entry in manifest)
        {
            var thread = await _store.LoadThreadAtCheckpointAsync(threadId, entry.CheckpointId, cancellationToken);
            if (thread == null)
                continue;

            // Accept checkpoints with or without ExecutionState
            // Root checkpoints (messageIndex=-1) don't have ExecutionState, that's expected
            // All other checkpoints should have ExecutionState, but we handle gracefully if missing
            result.Add(new CheckpointTuple
            {
                CheckpointId = entry.CheckpointId,
                CreatedAt = entry.CreatedAt,
                State = thread.ExecutionState,
                Metadata = thread.ExecutionState?.Metadata ?? new CheckpointMetadata
                {
                    Source = entry.Source,
                    Step = entry.Step,
                    ParentCheckpointId = entry.ParentCheckpointId,
                    BranchName = entry.BranchName,
                    MessageIndex = entry.MessageIndex
                },
                ParentCheckpointId = entry.ParentCheckpointId,
                BranchName = entry.BranchName,
                MessageIndex = entry.MessageIndex
            });
        }

        return result;
    }

    /// <summary>
    /// Get all checkpoint variants that diverge at a specific message index.
    /// Useful for ChatGPT-style "1/3" variant navigation UI.
    /// </summary>
    public async Task<List<CheckpointTuple>> GetVariantsAtMessageAsync(
        string threadId,
        int messageIndex,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);

        // Find checkpoints at this message index
        var atIndex = manifest.Where(c => c.MessageIndex == messageIndex).ToList();

        // Group by parent to find siblings (variants)
        var parentGroups = atIndex.GroupBy(c => c.ParentCheckpointId);

        // Get entries that share a parent (siblings = variants)
        var variantEntries = parentGroups
            .Where(g => g.Count() > 1 || g.Key != null)
            .SelectMany(g => g)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        // Load full checkpoint data
        var result = new List<CheckpointTuple>();
        foreach (var entry in variantEntries)
        {
            var thread = await _store.LoadThreadAtCheckpointAsync(threadId, entry.CheckpointId, cancellationToken);
            if (thread == null)
                continue;

            // Accept checkpoints with or without ExecutionState
            // Root checkpoints (messageIndex=-1) don't have ExecutionState, that's expected
            // All other checkpoints should have ExecutionState, but we handle gracefully if missing
            result.Add(new CheckpointTuple
            {
                CheckpointId = entry.CheckpointId,
                CreatedAt = entry.CreatedAt,
                State = thread.ExecutionState,
                Metadata = thread.ExecutionState?.Metadata ?? new CheckpointMetadata
                {
                    Source = entry.Source,
                    Step = entry.Step,
                    ParentCheckpointId = entry.ParentCheckpointId,
                    BranchName = entry.BranchName,
                    MessageIndex = entry.MessageIndex
                },
                ParentCheckpointId = entry.ParentCheckpointId,
                BranchName = entry.BranchName,
                MessageIndex = entry.MessageIndex
            });
        }

        return result;
    }

    //──────────────────────────────────────────────────────────────────
    // BRANCH MANAGEMENT
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delete a branch and optionally prune orphaned checkpoints.
    /// </summary>
    public async Task<BranchDeletedEvent?> DeleteBranchAsync(
        string threadId,
        string branchName,
        bool pruneOrphanedCheckpoints = true,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);

        // Check if branch exists
        var branchCheckpoints = manifest.Where(c => c.BranchName == branchName).ToList();
        if (branchCheckpoints.Count == 0)
            return null;

        // Remove branch label from checkpoints
        foreach (var cp in branchCheckpoints)
        {
            await _store.UpdateCheckpointManifestEntryAsync(
                threadId,
                cp.CheckpointId,
                entry => entry.BranchName = null,
                cancellationToken);
        }

        var checkpointsPruned = 0;

        if (pruneOrphanedCheckpoints)
        {
            checkpointsPruned = await PruneOrphanedCheckpointsAsync(threadId, cancellationToken);
        }

        // Create the event
        var evt = new BranchDeletedEvent(threadId, branchName, checkpointsPruned);

        // Notify observers
        NotifyObservers(o => o.OnBranchDeleted(evt));

        return evt;
    }

    /// <summary>
    /// Rename a branch.
    /// </summary>
    public async Task<BranchRenamedEvent?> RenameBranchAsync(
        string threadId,
        string oldName,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);

        // Check if newName already exists
        if (manifest.Any(c => c.BranchName == newName))
            return null;

        // Find checkpoints with oldName
        var toRename = manifest.Where(c => c.BranchName == oldName).ToList();
        if (toRename.Count == 0)
            return null;

        // Rename
        foreach (var cp in toRename)
        {
            await _store.UpdateCheckpointManifestEntryAsync(
                threadId,
                cp.CheckpointId,
                entry => entry.BranchName = newName,
                cancellationToken);
        }

        // Create the event
        var evt = new BranchRenamedEvent(threadId, oldName, newName);

        // Notify observers
        NotifyObservers(o => o.OnBranchRenamed(evt));

        return evt;
    }

    //──────────────────────────────────────────────────────────────────
    // CLEANUP
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Remove checkpoints not reachable from any named branch.
    /// </summary>
    public async Task<int> PruneOrphanedCheckpointsAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("Branching is not enabled");

        var manifest = await _store.GetCheckpointManifestAsync(threadId, cancellationToken: cancellationToken);
        if (manifest.Count == 0)
            return 0;

        // Find all reachable checkpoints from named branches
        var branchHeads = manifest
            .Where(c => c.BranchName != null)
            .Select(c => c.CheckpointId)
            .ToHashSet();

        var reachable = new HashSet<string>();
        var manifestLookup = manifest.ToDictionary(c => c.CheckpointId);

        foreach (var head in branchHeads)
        {
            var current = head;
            while (current != null && !reachable.Contains(current))
            {
                reachable.Add(current);
                if (manifestLookup.TryGetValue(current, out var cp))
                {
                    current = cp.ParentCheckpointId;
                }
                else
                {
                    break;
                }
            }
        }

        // Find unreachable checkpoints
        var toDelete = manifest
            .Where(c => !reachable.Contains(c.CheckpointId))
            .Select(c => c.CheckpointId)
            .ToList();

        if (toDelete.Count > 0)
        {
            await _store.DeleteCheckpointsAsync(threadId, toDelete, cancellationToken);
        }

        return toDelete.Count;
    }

    /// <summary>
    /// Compact storage by removing unreachable checkpoints.
    /// </summary>
    public Task<int> CompactAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        // Compact is same as prune orphaned
        return PruneOrphanedCheckpointsAsync(threadId, cancellationToken);
    }

    //──────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    //──────────────────────────────────────────────────────────────────

    private void NotifyObservers(Action<ICheckpointObserver> action)
    {
        foreach (var observer in _observers)
        {
            try
            {
                action(observer);
            }
            catch
            {
                // Don't let observer errors break the operation
            }
        }
    }
}

/// <summary>
/// Observer interface for branching and copy events.
/// </summary>
public interface ICheckpointObserver
{
    /// <summary>Called when a new branch is created (fork within same thread).</summary>
    void OnBranchCreated(BranchCreatedEvent evt);

    /// <summary>Called when the active branch is switched.</summary>
    void OnBranchSwitched(BranchSwitchedEvent evt);

    /// <summary>Called when a branch is deleted.</summary>
    void OnBranchDeleted(BranchDeletedEvent evt);

    /// <summary>Called when a branch is renamed.</summary>
    void OnBranchRenamed(BranchRenamedEvent evt);

    /// <summary>Called when a thread is copied from another thread's checkpoint.</summary>
    void OnThreadCopied(ThreadCopiedEvent evt);
}
