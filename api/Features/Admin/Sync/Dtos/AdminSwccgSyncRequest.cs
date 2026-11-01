using System.ComponentModel.DataAnnotations;

namespace api.Features.Admin.Sync.Dtos;

public sealed record AdminSwccgSyncRequest(
    bool DryRun = false,
    [param: Range(1, int.MaxValue)] int? Limit = null);
