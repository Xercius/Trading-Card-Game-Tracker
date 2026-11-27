namespace api.Features.Decks.Dtos;

public sealed record DecklistImportPreviewRequest(string Text);

public sealed record DecklistPrintingCandidateDto(
    int PrintingId,
    int CardId,
    string CardName,
    string? Subtitle,
    string? SetCode,
    string? SetName,
    string Number,
    string Style,
    string? ImageUrl);

public sealed record DecklistImportLineResult(
    int LineNumber,
    string RawLine,
    int Quantity,
    string ParsedName,
    string? ParsedSet,
    string? ParsedNumber,
    string Zone,
    string Status,
    int? SelectedPrintingId,
    IReadOnlyList<DecklistPrintingCandidateDto> Candidates,
    string? Message);

public sealed record DecklistImportPreviewResponse(
    IReadOnlyList<DecklistImportLineResult> Lines,
    int ResolvedCount,
    int AmbiguousCount,
    int UnmatchedCount);

public sealed record DecklistImportApplyItem(
    int CardPrintingId,
    int Quantity,
    string Zone);

public sealed record DecklistImportApplyRequest(
    IReadOnlyList<DecklistImportApplyItem> Items,
    bool ReplaceExisting = false);

public sealed record DecklistImportApplyResponse(
    int AppliedLines,
    int TotalCopies);

public sealed record DecklistExportTextResponse(
    string DeckName,
    string Game,
    string? Format,
    string Text,
    string FileName);
