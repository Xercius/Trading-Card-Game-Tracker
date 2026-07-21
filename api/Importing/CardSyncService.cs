using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

/// <summary>
/// Loads Star Wars: Unlimited cards modified since the last sync and compares them to the local SWU tables.
/// </summary>
internal sealed class CardSyncService : ICardSyncService
{
    private const string SupportedImporterKey = "swu";

    private readonly AppDbContext _db;
    private readonly ISWUApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardSyncService"/> class.
    /// </summary>
    /// <param name="db">Database context containing sync history and SWU entities.</param>
    /// <param name="client">SWU API client used to fetch modified cards.</param>
    public CardSyncService(AppDbContext db, ISWUApiClient client)
    {
        _db = db;
        _client = client;
    }

    /// <inheritdoc />
    public async Task<ImportSummary> SyncNewAndUpdatedCardsAsync(
        string importerKey,
        string? setCode = null,
        DateTimeOffset? updatedSince = null,
        bool forceFullSync = false,
        int? limit = null,
        CancellationToken ct = default)
    {
        ValidateRequest(importerKey, setCode);

        try
        {
            var effectiveUpdatedSince = forceFullSync
                ? null
                : updatedSince ?? await GetLastSyncTimeAsync(importerKey, setCode, ct);

            var records = await LoadRecordsAsync(setCode!, effectiveUpdatedSince, limit, ct);
            return await BuildSummaryAsync(importerKey, setCode!, records, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ImportSummary
            {
                Source = importerKey,
                DryRun = false,
                Errors = 1,
                Messages = new List<string> { $"Sync failed: {ex.Message}" }
            };
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastSyncTimeAsync(
        string importerKey,
        string? setCode = null,
        CancellationToken ct = default)
    {
        var normalizedSetCode = NormalizeSetCode(setCode);

        return await _db.ImportSyncHistories
            .AsNoTracking()
            .Where(h => h.ImporterKey == importerKey && h.SetCode == normalizedSetCode)
            .Select(h => (DateTimeOffset?)h.LastSyncedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateLastSyncTimeAsync(
        string importerKey,
        string? setCode,
        DateTimeOffset syncedAt,
        CancellationToken ct = default)
    {
        var normalizedSetCode = NormalizeSetCode(setCode);
        var existing = await _db.ImportSyncHistories
            .FirstOrDefaultAsync(h => h.ImporterKey == importerKey && h.SetCode == normalizedSetCode, ct);

        if (existing is null)
        {
            _db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = importerKey,
                SetCode = normalizedSetCode,
                LastSyncedAt = syncedAt
            });
        }
        else
        {
            existing.LastSyncedAt = syncedAt;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<StrapiRecord>> LoadRecordsAsync(
        string setCode,
        DateTimeOffset? updatedSince,
        int? limit,
        CancellationToken ct)
    {
        var normalizedSetCode = NormalizeSetCode(setCode).ToUpperInvariant();
        var expansionId = await _client.TryResolveExpansionIdAsync(normalizedSetCode, ct);
        var filter = new SWUCardFilter(
            ExpansionCode: expansionId is null ? normalizedSetCode : null,
            ExpansionId: expansionId,
            UpdatedSince: updatedSince);

        var records = await _client.GetAllCardsAsync(filter, ct);
        if (limit is null)
        {
            return records;
        }

        return limit <= 0
            ? Array.Empty<StrapiRecord>()
            : records.Take(limit.Value).ToArray();
    }

    private async Task<ImportSummary> BuildSummaryAsync(
        string importerKey,
        string setCode,
        IReadOnlyList<StrapiRecord> records,
        CancellationToken ct)
    {
        var summary = new ImportSummary
        {
            Source = importerKey,
            DryRun = false
        };

        var comparableRecords = new List<MappedRecord>(records.Count);
        foreach (var record in records)
        {
            if (!TryMapRecord(record, setCode, out var mappedRecord, out var skipMessage))
            {
                if (skipMessage is not null)
                {
                    summary.Messages.Add(skipMessage);
                }

                continue;
            }

            comparableRecords.Add(mappedRecord);
        }

        if (comparableRecords.Count == 0)
        {
            return summary;
        }

        var setCodes = comparableRecords
            .Select(r => r.SetCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var printingIds = comparableRecords
            .Select(r => r.PrintingStrapiId)
            .Distinct()
            .ToArray();
        var cardStrapiIds = comparableRecords
            .Select(r => r.CardStrapiId)
            .Distinct()
            .ToArray();
        var cardUids = comparableRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.CardUid))
            .Select(r => r.CardUid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var setsByCode = await _db.SwuSets
            .AsNoTracking()
            .Where(s => setCodes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, StringComparer.OrdinalIgnoreCase, ct);
        var printingsByStrapiId = await _db.SwuCardPrintings
            .AsNoTracking()
            .Include(p => p.SwuCard)
            .Include(p => p.SwuSet)
            .Where(p => printingIds.Contains(p.StrapiId))
            .ToDictionaryAsync(p => p.StrapiId, ct);
        var cardsByStrapiId = await _db.SwuCards
            .AsNoTracking()
            .Include(c => c.SwuSet)
            .Where(c => cardStrapiIds.Contains(c.StrapiId))
            .ToDictionaryAsync(c => c.StrapiId, ct);
        var cardsByCardUid = await _db.SwuCards
            .AsNoTracking()
            .Include(c => c.SwuSet)
            .Where(c => c.CardUid != null && cardUids.Contains(c.CardUid))
            .ToDictionaryAsync(c => c.CardUid!, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var record in comparableRecords)
        {
            var existingPrinting = printingsByStrapiId.GetValueOrDefault(record.PrintingStrapiId);
            var existingCard = ResolveExistingCard(record, existingPrinting, cardsByStrapiId, cardsByCardUid);
            var existingSet = setsByCode.GetValueOrDefault(record.SetCode);

            if (existingCard is null)
            {
                summary.CardsCreated++;
            }
            else if (CardHasChanges(existingCard, existingSet, record))
            {
                summary.CardsUpdated++;
            }

            if (existingPrinting is null)
            {
                summary.PrintingsCreated++;
            }
            else if (PrintingHasChanges(existingPrinting, existingSet, record))
            {
                summary.PrintingsUpdated++;
            }
        }

        return summary;
    }

    private static bool TryMapRecord(
        StrapiRecord record,
        string requestedSetCode,
        out MappedRecord mappedRecord,
        out string? skipMessage)
    {
        mappedRecord = default;
        skipMessage = null;

        var attributes = record.Attributes;
        if (attributes is null)
        {
            skipMessage = $"Skipping record id={record.Id} because it has no attributes.";
            return false;
        }

        if (!string.Equals(attributes.Locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            skipMessage = $"Skipping record id={record.Id} with locale={attributes.Locale ?? "(null)"} (expected \"en\").";
            return false;
        }

        mappedRecord = new MappedRecord(
            CardStrapiId: record.Id,
            PrintingStrapiId: record.Id,
            CardUid: NullIfWhiteSpace(attributes.CardUid),
            Title: (attributes.Title ?? "Unknown").Trim(),
            Subtitle: NullIfWhiteSpace(attributes.Subtitle),
            CardType: attributes.Type?.Data?.Attributes?.Name ?? string.Empty,
            Description: NullIfWhiteSpace(attributes.Text),
            Arena: NullIfWhiteSpace(attributes.Arena),
            Cost: attributes.Cost,
            Power: attributes.Power,
            Health: attributes.Health,
            Artist: NullIfWhiteSpace(attributes.Artist),
            Aspects: JoinValues(attributes.Aspects),
            Traits: JoinValues(attributes.Traits),
            Keywords: JoinValues(attributes.Keywords),
            SetCode: ResolveSetCode(attributes, requestedSetCode),
            Number: NullIfWhiteSpace(attributes.SerialCode) ?? attributes.CardNumber?.ToString() ?? record.Id.ToString(),
            Rarity: NullIfWhiteSpace(attributes.Rarity) ?? "Unknown",
            Style: ResolveStyle(attributes),
            ImageUrl: ResolveFrontImage(attributes),
            BackImageUrl: ResolveBackImage(attributes),
            ApiCreatedAt: attributes.CreatedAt,
            ApiUpdatedAt: attributes.UpdatedAt);

        return true;
    }

    private static SwuCard? ResolveExistingCard(
        MappedRecord record,
        SwuCardPrinting? existingPrinting,
        IReadOnlyDictionary<int, SwuCard> cardsByStrapiId,
        IReadOnlyDictionary<string, SwuCard> cardsByCardUid)
    {
        if (existingPrinting?.SwuCard is not null)
        {
            return existingPrinting.SwuCard;
        }

        if (cardsByStrapiId.TryGetValue(record.CardStrapiId, out var existingCard))
        {
            return existingCard;
        }

        if (record.CardUid is not null && cardsByCardUid.TryGetValue(record.CardUid, out existingCard))
        {
            return existingCard;
        }

        return null;
    }

    private static bool CardHasChanges(SwuCard existingCard, SwuSet? existingSet, MappedRecord record) =>
        existingCard.CardUid != record.CardUid
        || existingCard.Title != record.Title
        || existingCard.Subtitle != record.Subtitle
        || existingCard.CardType != record.CardType
        || existingCard.Description != record.Description
        || existingCard.Arena != record.Arena
        || existingCard.Cost != record.Cost
        || existingCard.Power != record.Power
        || existingCard.Health != record.Health
        || existingCard.Artist != record.Artist
        || existingCard.Aspects != record.Aspects
        || existingCard.Traits != record.Traits
        || existingCard.Keywords != record.Keywords
        || existingCard.ApiCreatedAt != record.ApiCreatedAt
        || existingCard.ApiUpdatedAt != record.ApiUpdatedAt
        || (existingSet is not null && existingCard.SwuSetId != existingSet.Id);

    private static bool PrintingHasChanges(SwuCardPrinting existingPrinting, SwuSet? existingSet, MappedRecord record) =>
        existingPrinting.Number != record.Number
        || existingPrinting.Rarity != record.Rarity
        || existingPrinting.Style != record.Style
        || existingPrinting.ImageUrl != record.ImageUrl
        || existingPrinting.BackImageUrl != record.BackImageUrl
        || existingPrinting.ApiCreatedAt != record.ApiCreatedAt
        || existingPrinting.ApiUpdatedAt != record.ApiUpdatedAt
        || (existingSet is not null && existingPrinting.SwuSetId != existingSet.Id);

    private static string ResolveSetCode(SwuCardAttributes attributes, string requestedSetCode) =>
        NormalizeSetCode(attributes.Expansion?.Data?.Attributes?.Code ?? requestedSetCode).ToUpperInvariant();

    private static string ResolveStyle(SwuCardAttributes attributes) =>
        attributes.VariantTypes?.Data?.Any(v => v.Attributes?.Foil == true) == true ? "Foil" : "Standard";

    private static string? ResolveFrontImage(SwuCardAttributes attributes) =>
        attributes.ArtFront?.Data?.Attributes?.Url
        ?? attributes.ArtFront?.Data?.Attributes?.Formats?.Card?.Url
        ?? attributes.ArtFront?.Data?.Attributes?.Formats?.Thumbnail?.Url
        ?? attributes.ArtBack?.Data?.Attributes?.Url;

    private static string? ResolveBackImage(SwuCardAttributes attributes) =>
        attributes.ArtBack?.Data?.Attributes?.Url
        ?? attributes.ArtBack?.Data?.Attributes?.Formats?.Card?.Url
        ?? attributes.ArtBack?.Data?.Attributes?.Formats?.Thumbnail?.Url;

    private static string? JoinValues(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var parts = values
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        return parts.Length == 0 ? null : string.Join('|', parts);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSetCode(string? setCode) =>
        string.IsNullOrWhiteSpace(setCode) ? string.Empty : setCode.Trim();

    private static void ValidateRequest(string importerKey, string? setCode)
    {
        if (!string.Equals(importerKey, SupportedImporterKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"CardSyncService currently supports only the '{SupportedImporterKey}' importer.");
        }

        if (string.IsNullOrWhiteSpace(setCode))
        {
            throw new ArgumentException("SetCode is required for SWU syncs.", nameof(setCode));
        }
    }

    private readonly record struct MappedRecord(
        int CardStrapiId,
        int PrintingStrapiId,
        string? CardUid,
        string Title,
        string? Subtitle,
        string CardType,
        string? Description,
        string? Arena,
        int? Cost,
        int? Power,
        int? Health,
        string? Artist,
        string? Aspects,
        string? Traits,
        string? Keywords,
        string SetCode,
        string Number,
        string Rarity,
        string Style,
        string? ImageUrl,
        string? BackImageUrl,
        DateTimeOffset? ApiCreatedAt,
        DateTimeOffset? ApiUpdatedAt);
}
