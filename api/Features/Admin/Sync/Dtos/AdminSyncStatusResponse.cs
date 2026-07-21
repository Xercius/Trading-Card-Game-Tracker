namespace api.Features.Admin.Sync.Dtos;

public sealed record AdminSyncStatusResponse(
    string Source,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int SetCount,
    int Created,
    int Updated,
    int Invalid,
    IReadOnlyList<string> Messages);
