using api.Data;
using api.Features.ImportExport.Dtos;
using api.Infrastructure.Startup;
using api.Models;
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
    private const int UserId = DbSeeder.DefaultUserId;
    private static readonly EventId ImportCompletedEventId = new(4100, "UserImportCompleted");

    /// <summary>Exports the user's collection, wishlist, and decks as formatted JSON bytes.</summary>
    public async Task<(byte[] Bytes, string FileName)> ExportJsonAsync(CancellationToken ct)
    {
        var collection = await db.UserCards.AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderBy(x => x.CardPrintingId)
            .Select(x => new ExportCollectionItem(x.CardPrintingId, x.QuantityOwned, x.QuantityProxyOwned))
            .ToListAsync(ct);

        var wishlist = await db.UserCards.AsNoTracking()
            .Where(x => x.UserId == UserId && x.QuantityWanted > 0)
            .OrderBy(x => x.CardPrintingId)
            .Select(x => new ExportWishlistItem(x.CardPrintingId, x.QuantityWanted))
            .ToListAsync(ct);

        var deckEntities = await db.Decks.AsNoTracking()
            .Where(d => d.UserId == UserId)
            .Include(d => d.Cards)
            .OrderBy(d => d.Game).ThenBy(d => d.Name)
            .ToListAsync(ct);

        var decks = deckEntities.Select(d => new ExportDeck(
            d.Game,
            d.Name,
            d.Description,
            d.Cards.OrderBy(c => c.CardPrintingId)
                .Select(c => new ExportDeckCard(c.CardPrintingId, c.QuantityInDeck, c.QuantityIdea, c.QuantityAcquire, c.QuantityProxy))
                .ToList())).ToList();

        var payload = new ExportPayload(1, new { id = UserId }, collection, wishlist, decks);
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
            .Where(x => x.UserId == UserId)
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
            .Where(x => x.UserId == UserId && x.QuantityWanted > 0)
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
            .Where(dc => dc.Deck != null && dc.CardPrinting != null && dc.CardPrinting.Card != null && dc.Deck.UserId == UserId)
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

    /// <summary>Finds any referenced card printing IDs that do not exist in the catalog.</summary>
    public async Task<IReadOnlyList<int>> FindMissingPrintingIdsAsync(ImportPayload payload, CancellationToken ct)
    {
        var collection = payload.Collection ?? [];
        var wishlist = payload.Wishlist ?? [];
        var decks = payload.Decks ?? [];

        var allIds = collection.Select(c => c.CardPrintingId)
            .Concat(wishlist.Select(w => w.CardPrintingId))
            .Concat(decks.SelectMany(d => d.Cards.Select(c => c.CardPrintingId)))
            .ToHashSet();

        if (allIds.Count == 0)
            return Array.Empty<int>();

        var present = await db.CardPrintings.AsNoTracking()
            .Where(cp => allIds.Contains(cp.Id))
            .Select(cp => cp.Id)
            .ToListAsync(ct);
        return allIds.Except(present).ToList();
    }

    /// <summary>Applies a validated JSON import in merge or replace mode inside a transaction.</summary>
    public async Task<ImportJsonResponse> ImportJsonAsync(ImportPayload payload, bool replaceMode, CancellationToken ct)
    {
        var collection = payload.Collection ?? [];
        var wishlist = payload.Wishlist ?? [];
        var decks = payload.Decks ?? [];

        using var trx = await db.Database.BeginTransactionAsync(ct);

        if (replaceMode)
        {
            var existingDecks = await db.Decks.Where(d => d.UserId == UserId).ToListAsync(ct);
            if (existingDecks.Count > 0) db.Decks.RemoveRange(existingDecks);

            var existingUserCards = await db.UserCards.Where(uc => uc.UserId == UserId).ToListAsync(ct);
            if (existingUserCards.Count > 0) db.UserCards.RemoveRange(existingUserCards);

            await db.SaveChangesAsync(ct);
        }

        await ApplyUserCardsAsync(collection, wishlist, replaceMode, ct);
        await ApplyDecksAsync(decks, replaceMode, ct);

        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);

        logger.LogInformation(ImportCompletedEventId, "Imported user backup ({Mode}): {Collection} collection, {Wishlist} wishlist, {Decks} decks",
            replaceMode ? "replace" : "merge", collection.Count, wishlist.Count, decks.Count);

        return new ImportJsonResponse(new ImportSummaryCounts(
            collection.Count,
            wishlist.Count,
            decks.Count,
            decks.SelectMany(d => d.Cards).Count()));
    }

    private async Task ApplyUserCardsAsync(
        IReadOnlyList<ExportCollectionItem> collection,
        IReadOnlyList<ExportWishlistItem> wishlist,
        bool replaceMode,
        CancellationToken ct)
    {
        var userCardMap = await db.UserCards.Where(uc => uc.UserId == UserId).ToDictionaryAsync(uc => uc.CardPrintingId, ct);

        foreach (var c in collection)
        {
            var row = GetOrCreateUserCard(userCardMap, c.CardPrintingId);
            var owned = Math.Max(0, c.QtyOwned);
            var proxy = Math.Max(0, c.QtyProxyOwned);
            row.QuantityOwned = replaceMode ? owned : Math.Max(0, row.QuantityOwned + owned);
            row.QuantityProxyOwned = replaceMode ? proxy : Math.Max(0, row.QuantityProxyOwned + proxy);
        }

        foreach (var w in wishlist)
        {
            var row = GetOrCreateUserCard(userCardMap, w.CardPrintingId);
            var wanted = Math.Max(0, w.Qty);
            row.QuantityWanted = replaceMode ? wanted : Math.Max(row.QuantityWanted, wanted);
        }
    }

    private UserCard GetOrCreateUserCard(Dictionary<int, UserCard> map, int printingId)
    {
        if (map.TryGetValue(printingId, out var row))
            return row;

        row = new UserCard { UserId = UserId, CardPrintingId = printingId };
        map[printingId] = row;
        db.UserCards.Add(row);
        return row;
    }

    private async Task ApplyDecksAsync(IReadOnlyList<ExportDeck> decks, bool replaceMode, CancellationToken ct)
    {
        var decksByKey = await db.Decks.Where(d => d.UserId == UserId).Include(d => d.Cards)
            .ToDictionaryAsync(d => (d.Game, d.Name), ct);

        foreach (var d in decks)
        {
            if (!decksByKey.TryGetValue((d.Game, d.Name), out var deck))
            {
                deck = new Deck { UserId = UserId, Game = d.Game, Name = d.Name, Description = d.Description };
                decksByKey[(d.Game, d.Name)] = deck;
                db.Decks.Add(deck);
            }
            else if (replaceMode || d.Description != deck.Description)
            {
                deck.Description = d.Description;
            }

            ApplyDeckCards(deck, d.Cards, replaceMode);
        }
    }

    private void ApplyDeckCards(Deck deck, IReadOnlyList<ExportDeckCard> cards, bool replaceMode)
    {
        var existingCards = deck.Cards.ToDictionary(c => c.CardPrintingId);
        foreach (var card in cards)
        {
            if (!existingCards.TryGetValue(card.CardPrintingId, out var deckCard))
            {
                deckCard = new DeckCard { Deck = deck, CardPrintingId = card.CardPrintingId };
                existingCards[card.CardPrintingId] = deckCard;
                deck.Cards.Add(deckCard);
                db.DeckCards.Add(deckCard);
            }

            var inDeck = Math.Max(0, card.InDeck);
            var idea = Math.Max(0, card.Idea);
            var acquire = Math.Max(0, card.Acquire);
            var proxy = Math.Max(0, card.Proxy);

            deckCard.QuantityInDeck = replaceMode ? inDeck : Math.Max(0, deckCard.QuantityInDeck + inDeck);
            deckCard.QuantityIdea = replaceMode ? idea : Math.Max(0, deckCard.QuantityIdea + idea);
            deckCard.QuantityAcquire = replaceMode ? acquire : Math.Max(0, deckCard.QuantityAcquire + acquire);
            deckCard.QuantityProxy = replaceMode ? proxy : Math.Max(0, deckCard.QuantityProxy + proxy);
        }
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var needsQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        var v = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{v}\"" : v;
    }
}
