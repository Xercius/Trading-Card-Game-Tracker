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
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardSyncService"/> class.
    /// </summary>
    /// <param name="db">Database context containing sync history and SWU entities.</param>
    /// <param name="client">SWU API client used to fetch modified cards.</param>
    /// <param name="timeProvider">Time provider used to obtain the current UTC time.</param>
    public CardSyncService(AppDbContext db, ISWUApiClient client, TimeProvider timeProvider)
    {
        _db = db;
        _client = client;
        _timeProvider = timeProvider;
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

        var syncStartedAt = _timeProvider.GetUtcNow();

        try
        {
            var effectiveUpdatedSince = forceFullSync
                ? null
                : updatedSince ?? await GetLastSyncTimeAsync(importerKey, setCode, ct);

            var records = await LoadRecordsAsync(setCode!, effectiveUpdatedSince, limit, ct);
            var summary = await BuildSummaryAsync(importerKey, setCode!, records, ct);
            await UpdateLastSyncTimeAsync(importerKey, setCode, syncStartedAt, ct);
            return summary;
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

        var fallbackSyncTime = _timeProvider.GetUtcNow();
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
            .Where(s => setCodes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, StringComparer.OrdinalIgnoreCase, ct);
        var printingsByStrapiId = await _db.SwuCardPrintings
            .Include(p => p.SwuCard)
            .Include(p => p.SwuSet)
            .Where(p => printingIds.Contains(p.StrapiId))
            .ToDictionaryAsync(p => p.StrapiId, ct);
        var cardsByStrapiId = await _db.SwuCards
            .Include(c => c.SwuSet)
            .Where(c => cardStrapiIds.Contains(c.StrapiId))
            .ToDictionaryAsync(c => c.StrapiId, ct);
        var cardsByCardUid = await _db.SwuCards
            .Include(c => c.SwuSet)
            .Where(c => c.CardUid != null && cardUids.Contains(c.CardUid))
            .ToDictionaryAsync(c => c.CardUid!, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var record in comparableRecords)
        {
            var rowSyncTime = record.ApiUpdatedAt ?? record.ApiCreatedAt ?? fallbackSyncTime;
            var swuSet = UpsertSet(record, rowSyncTime, setsByCode);
            var existingPrinting = printingsByStrapiId.GetValueOrDefault(record.PrintingStrapiId);
            var existingCard = ResolveExistingCard(record, existingPrinting, cardsByStrapiId, cardsByCardUid);

            if (existingCard is null)
            {
                existingCard = CreateCard(record, swuSet, rowSyncTime);
                _db.SwuCards.Add(existingCard);
                cardsByStrapiId[record.CardStrapiId] = existingCard;
                if (record.CardUid is not null)
                {
                    cardsByCardUid[record.CardUid] = existingCard;
                }

                summary.CardsCreated++;
            }
            else
            {
                if (UpdateCard(existingCard, swuSet, record, rowSyncTime))
                {
                    summary.CardsUpdated++;
                }

                if (record.CardUid is not null)
                {
                    cardsByCardUid[record.CardUid] = existingCard;
                }
            }

            if (existingPrinting is null)
            {
                existingPrinting = CreatePrinting(record, existingCard, swuSet, rowSyncTime);
                _db.SwuCardPrintings.Add(existingPrinting);
                printingsByStrapiId[record.PrintingStrapiId] = existingPrinting;
                summary.PrintingsCreated++;
            }
            else if (UpdatePrinting(existingPrinting, existingCard, swuSet, record, rowSyncTime))
            {
                summary.PrintingsUpdated++;
            }
        }

        await _db.SaveChangesAsync(ct);
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
            SetName: NullIfWhiteSpace(attributes.Expansion?.Data?.Attributes?.Name),
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

    private SwuSet UpsertSet(
        MappedRecord record,
        DateTimeOffset rowSyncTime,
        IDictionary<string, SwuSet> setsByCode)
    {
        if (setsByCode.TryGetValue(record.SetCode, out var existingSet))
        {
            if (existingSet.Name != record.SetName)
            {
                existingSet.Name = record.SetName;
                existingSet.LastSyncedAt = rowSyncTime;
            }

            return existingSet;
        }

        var swuSet = new SwuSet
        {
            Code = record.SetCode,
            Name = record.SetName,
            LastSyncedAt = rowSyncTime
        };
        _db.SwuSets.Add(swuSet);
        setsByCode[record.SetCode] = swuSet;
        return swuSet;
    }

    private static SwuCard CreateCard(MappedRecord record, SwuSet swuSet, DateTimeOffset rowSyncTime) =>
        new()
        {
            StrapiId = record.CardStrapiId,
            CardUid = record.CardUid,
            Title = record.Title,
            Subtitle = record.Subtitle,
            CardType = record.CardType,
            Description = record.Description,
            Arena = record.Arena,
            Cost = record.Cost,
            Power = record.Power,
            Health = record.Health,
            Artist = record.Artist,
            Aspects = record.Aspects,
            Traits = record.Traits,
            Keywords = record.Keywords,
            SwuSet = swuSet,
            ApiCreatedAt = record.ApiCreatedAt,
            ApiUpdatedAt = record.ApiUpdatedAt,
            LastSyncedAt = rowSyncTime
        };

    private static bool UpdateCard(SwuCard existingCard, SwuSet swuSet, MappedRecord record, DateTimeOffset rowSyncTime)
    {
        var changed = false;

        if (existingCard.CardUid != record.CardUid) { existingCard.CardUid = record.CardUid; changed = true; }
        if (existingCard.Title != record.Title) { existingCard.Title = record.Title; changed = true; }
        if (existingCard.Subtitle != record.Subtitle) { existingCard.Subtitle = record.Subtitle; changed = true; }
        if (existingCard.CardType != record.CardType) { existingCard.CardType = record.CardType; changed = true; }
        if (existingCard.Description != record.Description) { existingCard.Description = record.Description; changed = true; }
        if (existingCard.Arena != record.Arena) { existingCard.Arena = record.Arena; changed = true; }
        if (existingCard.Cost != record.Cost) { existingCard.Cost = record.Cost; changed = true; }
        if (existingCard.Power != record.Power) { existingCard.Power = record.Power; changed = true; }
        if (existingCard.Health != record.Health) { existingCard.Health = record.Health; changed = true; }
        if (existingCard.Artist != record.Artist) { existingCard.Artist = record.Artist; changed = true; }
        if (existingCard.Aspects != record.Aspects) { existingCard.Aspects = record.Aspects; changed = true; }
        if (existingCard.Traits != record.Traits) { existingCard.Traits = record.Traits; changed = true; }
        if (existingCard.Keywords != record.Keywords) { existingCard.Keywords = record.Keywords; changed = true; }
        if (existingCard.ApiCreatedAt != record.ApiCreatedAt) { existingCard.ApiCreatedAt = record.ApiCreatedAt; changed = true; }
        if (existingCard.ApiUpdatedAt != record.ApiUpdatedAt) { existingCard.ApiUpdatedAt = record.ApiUpdatedAt; changed = true; }
        if (existingCard.SwuSetId != swuSet.Id || !ReferenceEquals(existingCard.SwuSet, swuSet))
        {
            existingCard.SwuSet = swuSet;
            changed = true;
        }

        if (changed)
        {
            existingCard.LastSyncedAt = rowSyncTime;
        }

        return changed;
    }

    private static SwuCardPrinting CreatePrinting(
        MappedRecord record,
        SwuCard swuCard,
        SwuSet swuSet,
        DateTimeOffset rowSyncTime) =>
        new()
        {
            StrapiId = record.PrintingStrapiId,
            SwuCard = swuCard,
            SwuSet = swuSet,
            Number = record.Number,
            Rarity = record.Rarity,
            Style = record.Style,
            ImageUrl = record.ImageUrl,
            BackImageUrl = record.BackImageUrl,
            ApiCreatedAt = record.ApiCreatedAt,
            ApiUpdatedAt = record.ApiUpdatedAt,
            LastSyncedAt = rowSyncTime
        };

    private static bool UpdatePrinting(
        SwuCardPrinting existingPrinting,
        SwuCard swuCard,
        SwuSet swuSet,
        MappedRecord record,
        DateTimeOffset rowSyncTime)
    {
        var changed = false;

        if (existingPrinting.SwuCardId != swuCard.Id || !ReferenceEquals(existingPrinting.SwuCard, swuCard))
        {
            existingPrinting.SwuCard = swuCard;
            changed = true;
        }

        if (existingPrinting.SwuSetId != swuSet.Id || !ReferenceEquals(existingPrinting.SwuSet, swuSet))
        {
            existingPrinting.SwuSet = swuSet;
            changed = true;
        }

        if (existingPrinting.Number != record.Number) { existingPrinting.Number = record.Number; changed = true; }
        if (existingPrinting.Rarity != record.Rarity) { existingPrinting.Rarity = record.Rarity; changed = true; }
        if (existingPrinting.Style != record.Style) { existingPrinting.Style = record.Style; changed = true; }
        if (existingPrinting.ImageUrl != record.ImageUrl) { existingPrinting.ImageUrl = record.ImageUrl; changed = true; }
        if (existingPrinting.BackImageUrl != record.BackImageUrl) { existingPrinting.BackImageUrl = record.BackImageUrl; changed = true; }
        if (existingPrinting.ApiCreatedAt != record.ApiCreatedAt) { existingPrinting.ApiCreatedAt = record.ApiCreatedAt; changed = true; }
        if (existingPrinting.ApiUpdatedAt != record.ApiUpdatedAt) { existingPrinting.ApiUpdatedAt = record.ApiUpdatedAt; changed = true; }

        if (changed)
        {
            existingPrinting.LastSyncedAt = rowSyncTime;
        }

        return changed;
    }

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
        string? SetName,
        string Number,
        string Rarity,
        string Style,
        string? ImageUrl,
        string? BackImageUrl,
        DateTimeOffset? ApiCreatedAt,
        DateTimeOffset? ApiUpdatedAt);
}
