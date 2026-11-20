namespace api.Features.Admin.Sync.Dtos;

/// <summary>Describes a single SWCCG CSV row that failed validation or card matching.</summary>
public sealed record SwccgCsvRowIssue(
    string FileName,
    int RowNumber,
    string CardName,
    string Side,
    string OriginalSet,
    string Expansion,
    string Reason);

/// <summary>Summary returned after syncing SWCCG new cards and alternate printings from CSV files.</summary>
public sealed record AdminSwccgAlternatePrintingsSyncResponse(
    string Source,
    string Status,
    bool DryRun,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TotalRowsProcessed,
    int NewCardsCreated,
    int PrintingsCreated,
    int SkippedExisting,
    int SetsCreated,
    int RaritiesCreated,
    int TreatmentsCreated,
    int FailedCount,
    IReadOnlyList<SwccgCsvRowIssue> Failures,
    IReadOnlyList<string> Messages);
