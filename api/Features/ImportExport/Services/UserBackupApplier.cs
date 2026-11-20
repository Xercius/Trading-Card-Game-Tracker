using api.Data;
using api.Features.ImportExport.Dtos;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Features.ImportExport.Services;

/// <summary>
/// Applies a resolved JSON backup inside an active database transaction.
/// </summary>
internal sealed class UserBackupApplier(AppDbContext db, TimeProvider timeProvider)
{
    internal sealed record ResolvedDeckCard(int PrintingId, ExportDeckCard Raw);
    internal sealed record ResolvedDeck(ExportDeck Raw, List<ResolvedDeckCard> Cards);
    internal sealed record ResolvedPlan(
        List<(int PrintingId, ExportCollectionItem Raw)> Collection,
        List<(int PrintingId, ExportWishlistItem Raw)> Wishlist,
        List<ResolvedDeck> Decks,
        List<int> LegacyMissingIds,
        List<ImportUnresolvedItem> Unresolved,
        int ResolvedByNaturalKey);

    public async Task<ImportJsonResponse> ApplyAsync(
        ResolvedPlan plan,
        bool replaceMode,
        CancellationToken ct)
    {
        if (replaceMode)
        {
            await ClearExistingUserDataAsync(ct);
        }

        var deckMap = await ApplyDecksAsync(plan.Decks, replaceMode, ct);
        await db.SaveChangesAsync(ct);

        await ApplyUserCardsAsync(plan.Collection, plan.Wishlist, deckMap, replaceMode, ct);
        await db.SaveChangesAsync(ct);

        var counts = new ImportSummaryCounts(
            plan.Collection.Count,
            plan.Wishlist.Count,
            plan.Decks.Count,
            plan.Decks.Sum(d => d.Cards.Count),
            plan.ResolvedByNaturalKey,
            plan.Unresolved.Count);

        return new ImportJsonResponse(counts, plan.Unresolved);
    }

    private async Task ClearExistingUserDataAsync(CancellationToken ct)
    {
        var existingSources = await db.WishlistSources.ToListAsync(ct);
        if (existingSources.Count > 0) db.WishlistSources.RemoveRange(existingSources);

        var existingDecks = await db.Decks.ToListAsync(ct);
        if (existingDecks.Count > 0) db.Decks.RemoveRange(existingDecks);

        var existingUserCards = await db.UserCards.ToListAsync(ct);
        if (existingUserCards.Count > 0) db.UserCards.RemoveRange(existingUserCards);

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyUserCardsAsync(
        IReadOnlyList<(int PrintingId, ExportCollectionItem Raw)> collection,
        IReadOnlyList<(int PrintingId, ExportWishlistItem Raw)> wishlist,
        Dictionary<(string Game, string Name), Deck> deckMap,
        bool replaceMode,
        CancellationToken ct)
    {
        var userCardMap = await db.UserCards.ToDictionaryAsync(uc => uc.CardPrintingId, ct);
        var existingSources = await db.WishlistSources.ToListAsync(ct);
        var sourceMap = existingSources.ToDictionary(s => (s.CardPrintingId, s.DeckId));

        foreach (var (printingId, c) in collection)
        {
            var row = GetOrCreateUserCard(userCardMap, printingId);
            var owned = Math.Max(0, c.QtyOwned);
            var proxy = Math.Max(0, c.QtyProxyOwned);
            row.QuantityOwned = replaceMode ? owned : Math.Max(0, row.QuantityOwned + owned);
            row.QuantityProxyOwned = replaceMode ? proxy : Math.Max(0, row.QuantityProxyOwned + proxy);
        }

        foreach (var (printingId, w) in wishlist)
        {
            var row = GetOrCreateUserCard(userCardMap, printingId);
            var wanted = Math.Max(0, w.Qty);
            row.QuantityWanted = replaceMode ? wanted : Math.Max(row.QuantityWanted, wanted);

            if (w.Sources is not { Count: > 0 }) continue;

            var aggregated = AggregateWishlistSources(w.Sources, w.Game, deckMap);
            foreach (var (key, qty) in aggregated)
            {
                if (qty > 0) UpsertWishlistSource(sourceMap, printingId, key.DeckId, qty, replaceMode);
            }

            var totalFromSources = sourceMap.Values.Where(s => s.CardPrintingId == printingId).Sum(s => s.Quantity);
            row.QuantityWanted = replaceMode ? Math.Max(wanted, totalFromSources) : Math.Max(row.QuantityWanted, totalFromSources);
        }
    }

    private readonly record struct DeckSourceKey(int? DeckId);

    private static Dictionary<DeckSourceKey, int> AggregateWishlistSources(
        IReadOnlyList<ExportWishlistSourceItem> sources,
        string? fallbackGame,
        Dictionary<(string Game, string Name), Deck> deckMap)
    {
        var grouped = new Dictionary<DeckSourceKey, int>();
        foreach (var src in sources)
        {
            var qty = Math.Max(0, src.Qty);
            if (qty <= 0) continue;

            int? deckId = null;
            if (!string.IsNullOrWhiteSpace(src.DeckName))
            {
                var gameKey = src.DeckGame ?? fallbackGame ?? string.Empty;
                if (deckMap.TryGetValue((gameKey, src.DeckName), out var deck))
                {
                    deckId = deck.Id;
                }
            }

            var key = new DeckSourceKey(deckId);
            grouped[key] = grouped.TryGetValue(key, out var existing) ? existing + qty : qty;
        }
        return grouped;
    }

    private void UpsertWishlistSource(
        Dictionary<(int CardPrintingId, int? DeckId), WishlistSource> sourceMap,
        int printingId,
        int? deckId,
        int qty,
        bool replaceMode)
    {
        var now = timeProvider.GetUtcNow();
        if (!sourceMap.TryGetValue((printingId, deckId), out var source))
        {
            source = new WishlistSource
            {
                CardPrintingId = printingId,
                DeckId = deckId,
                Quantity = qty,
                UpdatedAt = now
            };
            sourceMap[(printingId, deckId)] = source;
            db.WishlistSources.Add(source);
            return;
        }

        source.Quantity = replaceMode ? qty : Math.Max(source.Quantity, qty);
        source.UpdatedAt = now;
    }

    private UserCard GetOrCreateUserCard(Dictionary<int, UserCard> map, int printingId)
    {
        if (map.TryGetValue(printingId, out var row)) return row;

        row = new UserCard { CardPrintingId = printingId };
        map[printingId] = row;
        db.UserCards.Add(row);
        return row;
    }

    private async Task<Dictionary<(string Game, string Name), Deck>> ApplyDecksAsync(
        IReadOnlyList<ResolvedDeck> decks,
        bool replaceMode,
        CancellationToken ct)
    {
        var existing = await db.Decks.Include(d => d.Cards).ToListAsync(ct);
        var decksByKey = new Dictionary<(string Game, string Name), Deck>(new DeckKeyComparer());
        foreach (var d in existing) decksByKey.TryAdd((d.Game, d.Name), d);

        foreach (var entry in decks)
        {
            var d = entry.Raw;
            if (!decksByKey.TryGetValue((d.Game, d.Name), out var deck))
            {
                deck = new Deck
                {
                    Game = d.Game,
                    Name = d.Name,
                    Description = d.Description,
                    Format = d.Format,
                    Side = d.Side,
                    CreatedUtc = timeProvider.GetUtcNow()
                };
                decksByKey[(d.Game, d.Name)] = deck;
                db.Decks.Add(deck);
            }
            else
            {
                if (replaceMode || d.Description != deck.Description) deck.Description = d.Description;
                if (replaceMode || !string.IsNullOrWhiteSpace(d.Format)) deck.Format = d.Format;
                if (replaceMode || !string.IsNullOrWhiteSpace(d.Side)) deck.Side = d.Side;
                deck.UpdatedUtc = timeProvider.GetUtcNow();
            }

            ApplyDeckCards(deck, entry.Cards, replaceMode);
        }

        return decksByKey;
    }

    private void ApplyDeckCards(Deck deck, IReadOnlyList<ResolvedDeckCard> cards, bool replaceMode)
    {
        var existingCards = deck.Cards.ToDictionary(c => c.CardPrintingId);
        foreach (var resolved in cards)
        {
            var card = resolved.Raw;
            if (!existingCards.TryGetValue(resolved.PrintingId, out var deckCard))
            {
                deckCard = new DeckCard { Deck = deck, CardPrintingId = resolved.PrintingId };
                existingCards[resolved.PrintingId] = deckCard;
                deck.Cards.Add(deckCard);
                db.DeckCards.Add(deckCard);
            }

            var inDeck = Math.Max(0, card.InDeck);
            var sideboard = Math.Max(0, card.Sideboard);
            var idea = Math.Max(0, card.Idea);
            var acquire = Math.Max(0, card.Acquire);
            var proxy = Math.Max(0, card.Proxy);

            deckCard.QuantityInDeck = replaceMode ? inDeck : Math.Max(0, deckCard.QuantityInDeck + inDeck);
            deckCard.QuantitySideboard = replaceMode ? sideboard : Math.Max(0, deckCard.QuantitySideboard + sideboard);
            deckCard.QuantityIdea = replaceMode ? idea : Math.Max(0, deckCard.QuantityIdea + idea);
            deckCard.QuantityAcquire = replaceMode ? acquire : Math.Max(0, deckCard.QuantityAcquire + acquire);
            deckCard.QuantityProxy = replaceMode ? proxy : Math.Max(0, deckCard.QuantityProxy + proxy);
            deckCard.IsStarting = replaceMode ? card.IsStarting : (deckCard.IsStarting || card.IsStarting);
            deckCard.IsCommander = replaceMode ? card.IsCommander : (deckCard.IsCommander || card.IsCommander);
        }
    }

    private sealed class DeckKeyComparer : IEqualityComparer<(string Game, string Name)>
    {
        public bool Equals((string Game, string Name) x, (string Game, string Name) y) =>
            string.Equals(x.Game, y.Game, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Game, string Name) obj) =>
            HashCode.Combine(obj.Game.ToUpperInvariant(), obj.Name.ToUpperInvariant());
    }
}
