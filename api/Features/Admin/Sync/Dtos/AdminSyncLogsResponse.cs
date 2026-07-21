namespace api.Features.Admin.Sync.Dtos;

/// <summary>A single entry from the <c>SyncLogs</c> audit table.</summary>
public sealed record AdminSyncLogEntry(
    int Id,
    int? SwuSetId,
    string? SetCode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool IsIncremental,
    DateTimeOffset? UpdatedSince,
    int CardsReturned,
    int CardsUpserted,
    string Status,
    string? ErrorMessage);

/// <summary>Response payload for the sync-logs list endpoint.</summary>
public sealed record AdminSyncLogsResponse(
    string Source,
    int TotalCount,
    IReadOnlyList<AdminSyncLogEntry> Logs);
