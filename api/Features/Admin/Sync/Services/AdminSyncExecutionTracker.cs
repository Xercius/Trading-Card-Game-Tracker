using System.Collections.Concurrent;

namespace api.Features.Admin.Sync.Services;

public sealed class AdminSyncExecutionTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _runningSyncs = new(StringComparer.OrdinalIgnoreCase);

    public bool TryStart(string key, DateTimeOffset startedAt) => _runningSyncs.TryAdd(key, startedAt);

    public bool TryGetStartedAt(string key, out DateTimeOffset startedAt) => _runningSyncs.TryGetValue(key, out startedAt);

    public void Complete(string key) => _runningSyncs.TryRemove(key, out _);
}
