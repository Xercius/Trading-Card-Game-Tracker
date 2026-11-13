namespace api.Features.Admin.Sync.Dtos;

public sealed record AdminSwccgSyncRequest(
    bool DryRun = false,
    int? Limit = null);
