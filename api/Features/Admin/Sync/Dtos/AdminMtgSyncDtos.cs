namespace api.Features.Admin.Sync.Dtos;

/// <summary>Set summary entry returned in MTG admin sync status.</summary>
public sealed record AdminMtgSetEntry(
    int Id,
    string Code,
    string Name,
    string? Era,
    string? ReleaseDate,
    int CardCountInDb,
    bool IsSynced,
    DateTimeOffset? LastSyncedAt
);

/// <summary>Overall MTG admin sync status and cached set list.</summary>
public sealed record AdminMtgSyncStatusResponse(
    bool IsRunning,
    DateTimeOffset? RunningSince,
    DateTimeOffset? LastSetsSyncAt,
    DateTimeOffset? LastCardsSyncAt,
    string? LastOutcome,
    IReadOnlyList<AdminMtgSetEntry> Sets
);

/// <summary>Request payload for syncing MTG cards for selected set codes.</summary>
public sealed record AdminMtgCardsSyncRequest(
    List<string>? SetCodes,
    bool ForceFullSync = false,
    bool DryRun = false
);
