namespace api.Features.Admin.Sync.Dtos;

public sealed record AdminPokemonSetEntry(
    int Id,
    string Code,
    string Name,
    string? Era,
    string? ReleaseDate,
    int CardCountInDb,
    bool IsSynced,
    DateTimeOffset? LastSyncedAt
);

public sealed record AdminPokemonSyncStatusResponse(
    bool IsRunning,
    DateTimeOffset? RunningSince,
    DateTimeOffset? LastSetsSyncAt,
    DateTimeOffset? LastCardsSyncAt,
    string? LastOutcome,
    IReadOnlyList<AdminPokemonSetEntry> Sets
);

public sealed record AdminPokemonCardsSyncRequest(
    List<string>? SetCodes,
    bool ForceFullSync = false,
    bool DryRun = false
);
