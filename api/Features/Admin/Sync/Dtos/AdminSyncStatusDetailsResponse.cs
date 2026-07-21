namespace api.Features.Admin.Sync.Dtos;

public sealed record AdminSyncSetHistoryEntry(
    string SetCode,
    DateTimeOffset LastSyncedAt);

public sealed record AdminSyncStatusDetailsResponse(
    string Source,
    string Status,
    DateTimeOffset? RunningSince,
    DateTimeOffset? LastCompletedAt,
    int HistoryCount,
    IReadOnlyList<AdminSyncSetHistoryEntry> History,
    IReadOnlyList<string> Messages);
