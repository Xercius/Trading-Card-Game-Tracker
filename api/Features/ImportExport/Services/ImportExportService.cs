using api.Data;
using api.Features.ImportExport.Dtos;
using api.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace api.Features.ImportExport.Services;

/// <summary>Handles user collection, wishlist, and deck export/import operations.</summary>
public sealed class ImportExportService(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<ImportExportService> logger)
{
    private static readonly EventId ImportCompletedEventId = new(4100, "UserImportCompleted");

    /// <summary>Exports the user's collection, wishlist, and decks as formatted JSON bytes.</summary>
    public async Task<(byte[] Bytes, string FileName)> ExportJsonAsync(CancellationToken ct)
    {
        var collection = await LoadCollectionExportAsync(ct);
        var wishlist = await LoadWishlistExportAsync(ct);
        var decks = await LoadDecksExportAsync(ct);

        var payload = new ExportPayload(1, collection, wishlist, decks);
        var jsonOptions = JsonOptionsConfigurator.CreateSerializerOptions();
        jsonOptions.WriteIndented = true;
        jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, jsonOptions));
        var fileName = $"tcgtracker_export_utc_{timeProvider.GetUtcNow():yyyyMMddHHmmss}.json";
        return (bytes, fileName);
    }

    /// <summary>Exports the user's collection as CSV bytes.</summary>
    public async Task<byte[]> ExportCollectionCsvAsync(CancellationToken ct)
    {
        var rows = await db.UserCards.AsNoTracking()
            .OrderBy(r => r.CardPrinting.Card.Game.Name)
            .ThenBy(r => r.CardPrinting.Card.Name)
            .ThenBy(r => r.CardPrinting.CatalogSet == null ? string.Empty : r.CardPrinting.CatalogSet.Code)
            .ThenBy(r => r.CardPrinting.Number)
            .Select(x => new
            {
                Game = x.CardPrinting.Card.Game.Name,
                CardName = x.CardPrinting.Card.Name,
                Set = x.CardPrinting.CatalogSet == null ? string.Empty : x.CardPrinting.CatalogSet.Name ?? x.CardPrinting.CatalogSet.Code,
                x.CardPrinting.Number,
                Rarity = x.CardPrinting.RarityLookup == null ? string.Empty : x.CardPrinting.RarityLookup.Name,
                Style = x.CardPrinting.Treatment == null ? "Standard" : x.CardPrinting.Treatment.Name,
                x.CardPrintingId,
                x.QuantityOwned,
                x.QuantityProxyOwned
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Game,CardName,Set,Number,Rarity,Style,CardPrintingId,QuantityOwned,QuantityProxyOwned");
        foreach (var r in rows)
            sb.AppendLine($"{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.QuantityOwned},{r.QuantityProxyOwned}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Exports the user's wishlist as CSV bytes.</summary>
    public async Task<byte[]> ExportWishlistCsvAsync(CancellationToken ct)
    {
        var rows = await db.UserCards.AsNoTracking()
            .Where(x => x.QuantityWanted > 0)
            .OrderBy(r => r.CardPrinting.Card.Game.Name)
            .ThenBy(r => r.CardPrinting.Card.Name)
            .ThenBy(r => r.CardPrinting.CatalogSet == null ? string.Empty : r.CardPrinting.CatalogSet.Code)
            .ThenBy(r => r.CardPrinting.Number)
            .Select(x => new
            {
                Game = x.CardPrinting.Card.Game.Name,
                CardName = x.CardPrinting.Card.Name,
                Set = x.CardPrinting.CatalogSet == null ? string.Empty : x.CardPrinting.CatalogSet.Name ?? x.CardPrinting.CatalogSet.Code,
                x.CardPrinting.Number,
                Rarity = x.CardPrinting.RarityLookup == null ? string.Empty : x.CardPrinting.RarityLookup.Name,
                Style = x.CardPrinting.Treatment == null ? "Standard" : x.CardPrinting.Treatment.Name,
                x.CardPrintingId,
                Quantity = x.QuantityWanted
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Game,CardName,Set,Number,Rarity,Style,CardPrintingId,Quantity");
        foreach (var r in rows)
            sb.AppendLine($"{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.Quantity}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Exports the user's decks as CSV bytes.</summary>
    public async Task<byte[]> ExportDecksCsvAsync(CancellationToken ct)
    {
        var rows = await db.DeckCards.AsNoTracking()
            .Where(dc => dc.Deck != null && dc.CardPrinting != null && dc.CardPrinting.Card != null)
            .OrderBy(r => r.Deck!.Game).ThenBy(r => r.Deck!.Name)
            .ThenBy(r => r.CardPrinting!.Card.Name)
            .ThenBy(r => r.CardPrinting!.CatalogSet == null ? string.Empty : r.CardPrinting!.CatalogSet.Code)
            .ThenBy(r => r.CardPrinting!.Number ?? "")
            .Select(dc => new
            {
                DeckName = dc.Deck!.Name,
                Game = dc.Deck!.Game,
                CardName = dc.CardPrinting!.Card.Name,
                Set = dc.CardPrinting!.CatalogSet == null ? string.Empty : dc.CardPrinting.CatalogSet.Name ?? dc.CardPrinting.CatalogSet.Code,
                Number = dc.CardPrinting!.Number ?? "",
                Rarity = dc.CardPrinting!.RarityLookup == null ? string.Empty : dc.CardPrinting!.RarityLookup.Name,
                Style = dc.CardPrinting!.Treatment == null ? "Standard" : dc.CardPrinting!.Treatment.Name,
                dc.CardPrintingId,
                dc.QuantityInDeck,
                dc.QuantityIdea,
                dc.QuantityAcquire,
                dc.QuantityProxy
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("DeckName,Game,CardName,Set,Number,Rarity,Style,CardPrintingId,QuantityInDeck,QuantityIdea,QuantityAcquire,QuantityProxy");
        foreach (var r in rows)
            sb.AppendLine($"{Csv(r.DeckName)},{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.QuantityInDeck},{r.QuantityIdea},{r.QuantityAcquire},{r.QuantityProxy}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Validates and imports JSON backup data, resolving shifted CardPrintingIds via natural keys when supplied.
    /// Returns legacy missing IDs when an ID-only item cannot be resolved.
    /// </summary>
    public async Task<(ImportJsonResponse? Response, IReadOnlyList<int> LegacyMissingIds)> TryImportJsonAsync(
        ImportPayload payload,
        bool replaceMode,
        CancellationToken ct)
    {
        var resolver = await PrintingNaturalKeyResolver.LoadAsync(db, ct);
        var plan = BuildResolvedPlan(payload, resolver);
        if (plan.LegacyMissingIds.Count > 0)
        {
            return (null, plan.LegacyMissingIds);
        }

        using var trx = await db.Database.BeginTransactionAsync(ct);
        var applier = new UserBackupApplier(db, timeProvider);
        var response = await applier.ApplyAsync(plan, replaceMode, ct);
        await trx.CommitAsync(ct);

        logger.LogInformation(ImportCompletedEventId,
            "Imported user backup ({Mode}): {Collection} collection, {Wishlist} wishlist, {Decks} decks ({NaturalMatches} via natural key, {Unresolved} unresolved)",
            replaceMode ? "replace" : "merge", response.Imported.Collection, response.Imported.Wishlist,
            response.Imported.Decks, response.Imported.ResolvedByNaturalKey, response.Imported.UnresolvedCount);

        return (response, Array.Empty<int>());
    }

    private async Task<List<ExportCollectionItem>> LoadCollectionExportAsync(CancellationToken ct) =>
        await db.UserCards.AsNoTracking()
            .OrderBy(x => x.CardPrintingId)
            .Select(x => new ExportCollectionItem(
                x.CardPrintingId,
                x.QuantityOwned,
                x.QuantityProxyOwned,
                x.CardPrinting.Card.Game.Name,
                x.CardPrinting.Card.Name,
                x.CardPrinting.CatalogSet != null ? x.CardPrinting.CatalogSet.Code : null,
                x.CardPrinting.CatalogSet != null ? x.CardPrinting.CatalogSet.Name : null,
                x.CardPrinting.Number,
                x.CardPrinting.Treatment != null ? x.CardPrinting.Treatment.Name : "Standard"))
            .ToListAsync(ct);

    private async Task<List<ExportWishlistItem>> LoadWishlistExportAsync(CancellationToken ct)
    {
        var sourcesByPrinting = await db.WishlistSources.AsNoTracking()
            .Include(ws => ws.Deck)
            .OrderBy(ws => ws.DeckId)
            .ToListAsync(ct);
        var sourceLookup = sourcesByPrinting
            .GroupBy(ws => ws.CardPrintingId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ws => new ExportWishlistSourceItem(ws.Deck?.Game, ws.Deck?.Name, ws.Quantity)).ToList());

        var rows = await db.UserCards.AsNoTracking()
            .Where(x => x.QuantityWanted > 0)
            .OrderBy(x => x.CardPrintingId)
            .Select(x => new
            {
                x.CardPrintingId,
                x.QuantityWanted,
                Game = x.CardPrinting.Card.Game.Name,
                CardName = x.CardPrinting.Card.Name,
                SetCode = x.CardPrinting.CatalogSet != null ? x.CardPrinting.CatalogSet.Code : null,
                SetName = x.CardPrinting.CatalogSet != null ? x.CardPrinting.CatalogSet.Name : null,
                x.CardPrinting.Number,
                Style = x.CardPrinting.Treatment != null ? x.CardPrinting.Treatment.Name : "Standard"
            })
            .ToListAsync(ct);

        return rows.Select(x => new ExportWishlistItem(
            x.CardPrintingId,
            x.QuantityWanted,
            x.Game,
            x.CardName,
            x.SetCode,
            x.SetName,
            x.Number,
            x.Style,
            sourceLookup.TryGetValue(x.CardPrintingId, out var list) ? list : null)).ToList();
    }

    private async Task<List<ExportDeck>> LoadDecksExportAsync(CancellationToken ct)
    {
        var deckEntities = await db.Decks.AsNoTracking()
            .Include(d => d.Cards).ThenInclude(dc => dc.CardPrinting!).ThenInclude(cp => cp.Card)
            .Include(d => d.Cards).ThenInclude(dc => dc.CardPrinting!).ThenInclude(cp => cp.CatalogSet)
            .Include(d => d.Cards).ThenInclude(dc => dc.CardPrinting!).ThenInclude(cp => cp.Treatment)
            .AsSplitQuery()
            .OrderBy(d => d.Game).ThenBy(d => d.Name)
            .ToListAsync(ct);

        return deckEntities.Select(d => new ExportDeck(
            d.Game,
            d.Name,
            d.Description,
            d.Cards.OrderBy(c => c.CardPrintingId)
                .Select(c => new ExportDeckCard(
                    c.CardPrintingId,
                    c.QuantityInDeck,
                    c.QuantityIdea,
                    c.QuantityAcquire,
                    c.QuantityProxy,
                    c.QuantitySideboard,
                    c.IsStarting,
                    c.IsCommander,
                    c.CardPrinting?.Card.Name,
                    c.CardPrinting?.CatalogSet?.Code,
                    c.CardPrinting?.CatalogSet?.Name,
                    c.CardPrinting?.Number,
                    c.CardPrinting?.Treatment?.Name ?? "Standard"))
                .ToList(),
            d.Format,
            d.Side)).ToList();
    }

    private static UserBackupApplier.ResolvedPlan BuildResolvedPlan(ImportPayload payload, PrintingNaturalKeyResolver resolver)
    {
        var collection = new List<(int, ExportCollectionItem)>();
        var wishlist = new List<(int, ExportWishlistItem)>();
        var decks = new List<UserBackupApplier.ResolvedDeck>();
        var legacyMissing = new HashSet<int>();
        var unresolved = new List<ImportUnresolvedItem>();
        var naturalKeyHits = 0;

        foreach (var item in payload.Collection ?? [])
        {
            var res = resolver.Resolve(item.CardPrintingId, item.Game, item.CardName, item.SetCode, item.SetName, item.Number, item.Style);
            if (!res.ResolvedPrintingId.HasValue)
            {
                if (!res.HasNaturalKey) legacyMissing.Add(item.CardPrintingId);
                else unresolved.Add(new ImportUnresolvedItem("collection", item.CardPrintingId, item.Game, item.CardName, item.SetCode, item.Number, res.Reason!));
                continue;
            }
            if (res.UsedNaturalKey) naturalKeyHits++;
            collection.Add((res.ResolvedPrintingId.Value, item));
        }

        foreach (var item in payload.Wishlist ?? [])
        {
            var res = resolver.Resolve(item.CardPrintingId, item.Game, item.CardName, item.SetCode, item.SetName, item.Number, item.Style);
            if (!res.ResolvedPrintingId.HasValue)
            {
                if (!res.HasNaturalKey) legacyMissing.Add(item.CardPrintingId);
                else unresolved.Add(new ImportUnresolvedItem("wishlist", item.CardPrintingId, item.Game, item.CardName, item.SetCode, item.Number, res.Reason!));
                continue;
            }
            if (res.UsedNaturalKey) naturalKeyHits++;
            wishlist.Add((res.ResolvedPrintingId.Value, item));
        }

        foreach (var deck in payload.Decks ?? [])
        {
            var resolvedCards = new List<UserBackupApplier.ResolvedDeckCard>();
            foreach (var card in deck.Cards)
            {
                var res = resolver.Resolve(card.CardPrintingId, deck.Game, card.CardName, card.SetCode, card.SetName, card.Number, card.Style);
                if (!res.ResolvedPrintingId.HasValue)
                {
                    if (!res.HasNaturalKey) legacyMissing.Add(card.CardPrintingId);
                    else unresolved.Add(new ImportUnresolvedItem($"deck:{deck.Name}", card.CardPrintingId, deck.Game, card.CardName, card.SetCode, card.Number, res.Reason!));
                    continue;
                }
                if (res.UsedNaturalKey) naturalKeyHits++;
                resolvedCards.Add(new UserBackupApplier.ResolvedDeckCard(res.ResolvedPrintingId.Value, card));
            }
            decks.Add(new UserBackupApplier.ResolvedDeck(deck, resolvedCards));
        }

        return new UserBackupApplier.ResolvedPlan(collection, wishlist, decks, legacyMissing.ToList(), unresolved, naturalKeyHits);
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var needsQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        var v = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{v}\"" : v;
    }
}
