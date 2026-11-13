using System.Collections.Concurrent;

namespace api.Features.Admin.Sync.Services;

/// <summary>
/// Tracks in-flight admin sync jobs by route key to prevent overlapping runs.
/// </summary>
public sealed class AdminSyncExecutionTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _runningSyncs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Attempts to mark a sync key as running; returns false if already active.</summary>
    public bool TryStart(string key, DateTimeOffset startedAt) => _runningSyncs.TryAdd(key, startedAt);

    /// <summary>Gets the start timestamp for an active sync key, if present.</summary>
    public bool TryGetStartedAt(string key, out DateTimeOffset startedAt) => _runningSyncs.TryGetValue(key, out startedAt);

    /// <summary>Clears the active sync marker when a sync finishes or fails.</summary>
    public void Complete(string key) => _runningSyncs.TryRemove(key, out _);
}
