using System.Collections.Concurrent;
using System.Text.Json;

namespace HPD.Agent.Checkpointing;

/// <summary>
/// Simple in-memory thread store for development and testing.
/// Stores only the latest state per thread (no checkpoint history).
/// Data is lost on process restart.
/// </summary>
/// <remarks>
/// <para>
/// Use this for simple scenarios where you only need crash recovery,
/// not full checkpoint history, branching, or time-travel debugging.
/// </para>
/// <para>
/// For full checkpoint capabilities, use <see cref="InMemoryConversationThreadStore"/>.
/// </para>
/// </remarks>
public class InMemoryThreadStore : IThreadStore
{
    private readonly ConcurrentDictionary<string, JsonElement> _threads = new();

    public Task<ConversationThread?> LoadThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        if (_threads.TryGetValue(threadId, out var snapshotJson))
        {
            var snapshot = JsonSerializer.Deserialize(
                snapshotJson.GetRawText(),
                HPDJsonContext.Default.ConversationThreadSnapshot);

            if (snapshot != null)
            {
                var thread = ConversationThread.Deserialize(snapshot, null);
                return Task.FromResult<ConversationThread?>(thread);
            }
        }

        return Task.FromResult<ConversationThread?>(null);
    }

    public Task SaveThreadAsync(
        ConversationThread thread,
        CancellationToken cancellationToken = default)
    {
        var snapshot = thread.Serialize(null);
        var snapshotJson = JsonSerializer.SerializeToElement(
            snapshot,
            HPDJsonContext.Default.ConversationThreadSnapshot);

        _threads[thread.Id] = snapshotJson;
        return Task.CompletedTask;
    }

    public Task<List<string>> ListThreadIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_threads.Keys.ToList());
    }

    public Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        _threads.TryRemove(threadId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Delete threads that have been inactive for longer than the threshold.
    /// </summary>
    public Task<int> DeleteInactiveThreadsAsync(
        TimeSpan inactivityThreshold,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - inactivityThreshold;
        var toRemove = new List<string>();

        foreach (var kvp in _threads)
        {
            var snapshot = JsonSerializer.Deserialize(
                kvp.Value.GetRawText(),
                HPDJsonContext.Default.ConversationThreadSnapshot);

            if (snapshot != null && snapshot.LastActivity < cutoff)
            {
                toRemove.Add(kvp.Key);
            }
        }

        if (!dryRun)
        {
            foreach (var threadId in toRemove)
            {
                _threads.TryRemove(threadId, out _);
            }
        }

        return Task.FromResult(toRemove.Count);
    }
}
