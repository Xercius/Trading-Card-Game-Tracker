using api.Data;
using api.Features._Common;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Shared;

/// <summary>
/// Maintains <see cref="WishlistSource"/> rows and keeps <see cref="UserCard.QuantityWanted"/>
/// synchronized as the sum of all active sources for each printing.
/// </summary>
public sealed class WishlistSourceSyncService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public WishlistSourceSyncService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <summary>Sets the desired quantity for a specific wishlist source (<c>null</c> = General) and recalculates total wanted.</summary>
    public async Task<UserCard?> SetSourceQuantityAsync(
        int cardPrintingId,
        int? deckId,
        int quantity,
        CancellationToken ct = default)
    {
        var clamped = QuantityGuards.Clamp(quantity);
        var source = await _db.WishlistSources
            .FirstOrDefaultAsync(w => w.CardPrintingId == cardPrintingId && w.DeckId == deckId, ct);

        if (clamped == 0)
        {
            if (source is not null)
            {
                _db.WishlistSources.Remove(source);
            }
        }
        else if (source is null)
        {
            _db.WishlistSources.Add(new WishlistSource
            {
                CardPrintingId = cardPrintingId,
                DeckId = deckId,
                Quantity = clamped,
                UpdatedAt = _timeProvider.GetUtcNow()
            });
        }
        else
        {
            source.Quantity = clamped;
            source.UpdatedAt = _timeProvider.GetUtcNow();
        }

        await _db.SaveChangesAsync(ct);
        return await RecalculateWantedAsync(cardPrintingId, ct);
    }

    /// <summary>Applies a quantity delta to a specific wishlist source (<c>null</c> = General) and recalculates total wanted.</summary>
    public async Task<UserCard?> DeltaSourceQuantityAsync(
        int cardPrintingId,
        int? deckId,
        int delta,
        CancellationToken ct = default)
    {
        var source = await _db.WishlistSources
            .FirstOrDefaultAsync(w => w.CardPrintingId == cardPrintingId && w.DeckId == deckId, ct);

        var current = source?.Quantity ?? 0;
        if (source is null && deckId is null)
        {
            var hasAnySources = await _db.WishlistSources
                .AnyAsync(w => w.CardPrintingId == cardPrintingId, ct);
            if (!hasAnySources)
            {
                var uc = await _db.UserCards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CardPrintingId == cardPrintingId, ct);
                current = uc?.QuantityWanted ?? 0;
            }
        }

        var next = QuantityGuards.ClampDelta(current, delta);
        return await SetSourceQuantityAsync(cardPrintingId, deckId, next, ct);
    }

    /// <summary>Removes all wishlist sources for a printing and clears <see cref="UserCard.QuantityWanted"/>.</summary>
    public async Task RemoveAllSourcesForPrintingAsync(int cardPrintingId, CancellationToken ct = default)
    {
        var sources = await _db.WishlistSources
            .Where(w => w.CardPrintingId == cardPrintingId)
            .ToListAsync(ct);

        if (sources.Count > 0)
        {
            _db.WishlistSources.RemoveRange(sources);
            await _db.SaveChangesAsync(ct);
        }

        await RecalculateWantedAsync(cardPrintingId, ct);
    }

    /// <summary>Removes all wishlist sources linked to <paramref name="deckId"/> and recalculates affected printings.</summary>
    public async Task RemoveDeckSourcesAsync(int deckId, CancellationToken ct = default)
    {
        var sources = await _db.WishlistSources
            .Where(w => w.DeckId == deckId)
            .ToListAsync(ct);

        if (sources.Count == 0) return;

        var affectedPrintingIds = sources.Select(s => s.CardPrintingId).Distinct().ToList();
        _db.WishlistSources.RemoveRange(sources);
        await _db.SaveChangesAsync(ct);

        foreach (var printingId in affectedPrintingIds)
        {
            await RecalculateWantedAsync(printingId, ct);
        }
    }

    /// <summary>
    /// Reconciles <see cref="WishlistSource"/> rows for <paramref name="cardPrintingId"/> so their sum equals
    /// <paramref name="targetWanted"/>, adjusting the General source first and trimming deck sources only when needed.
    /// Does not call <c>SaveChangesAsync</c> so callers can commit atomically with other changes.
    /// </summary>
    public async Task SyncSourcesToTotalWantedAsync(
        int cardPrintingId,
        int targetWanted,
        CancellationToken ct = default)
    {
        var clamped = QuantityGuards.Clamp(targetWanted);
        var sources = await _db.WishlistSources
            .Where(w => w.CardPrintingId == cardPrintingId)
            .OrderBy(w => w.DeckId)
            .ToListAsync(ct);

        if (clamped == 0)
        {
            if (sources.Count > 0)
            {
                _db.WishlistSources.RemoveRange(sources);
            }
            return;
        }

        var general = sources.FirstOrDefault(s => s.DeckId is null);
        var deckSources = sources.Where(s => s.DeckId is not null).ToList();
        var deckTotal = deckSources.Sum(s => s.Quantity);
        var now = _timeProvider.GetUtcNow();

        if (clamped >= deckTotal)
        {
            var generalTarget = clamped - deckTotal;
            if (generalTarget == 0)
            {
                if (general is not null) _db.WishlistSources.Remove(general);
            }
            else if (general is null)
            {
                _db.WishlistSources.Add(new WishlistSource
                {
                    CardPrintingId = cardPrintingId,
                    DeckId = null,
                    Quantity = generalTarget,
                    UpdatedAt = now
                });
            }
            else
            {
                general.Quantity = generalTarget;
                general.UpdatedAt = now;
            }
            return;
        }

        if (general is not null) _db.WishlistSources.Remove(general);

        var excess = deckTotal - clamped;
        for (var i = deckSources.Count - 1; i >= 0 && excess > 0; i--)
        {
            var src = deckSources[i];
            var cut = Math.Min(src.Quantity, excess);
            src.Quantity -= cut;
            excess -= cut;
            if (src.Quantity == 0)
            {
                _db.WishlistSources.Remove(src);
            }
            else
            {
                src.UpdatedAt = now;
            }
        }
    }

    /// <summary>
    /// Migrates or merges a deck-linked <see cref="WishlistSource"/> when a deck card switches printings
    /// and recalculates <see cref="UserCard.QuantityWanted"/> for both printings.
    /// </summary>
    public async Task MigrateDeckSourceAsync(
        int deckId,
        int fromPrintingId,
        int toPrintingId,
        CancellationToken ct = default)
    {
        if (fromPrintingId == toPrintingId) return;

        var fromSource = await _db.WishlistSources
            .FirstOrDefaultAsync(w => w.DeckId == deckId && w.CardPrintingId == fromPrintingId, ct);
        if (fromSource is null) return;

        var toSource = await _db.WishlistSources
            .FirstOrDefaultAsync(w => w.DeckId == deckId && w.CardPrintingId == toPrintingId, ct);
        var now = _timeProvider.GetUtcNow();

        if (toSource is null)
        {
            fromSource.CardPrintingId = toPrintingId;
            fromSource.UpdatedAt = now;
        }
        else
        {
            toSource.Quantity = QuantityGuards.ClampDelta(toSource.Quantity, fromSource.Quantity);
            toSource.UpdatedAt = now;
            _db.WishlistSources.Remove(fromSource);
        }

        await _db.SaveChangesAsync(ct);
        await RecalculateWantedAsync(fromPrintingId, ct);
        await RecalculateWantedAsync(toPrintingId, ct);
    }

    /// <summary>Recalculates <see cref="UserCard.QuantityWanted"/> from <see cref="WishlistSource"/> rows.</summary>
    public async Task<UserCard?> RecalculateWantedAsync(int cardPrintingId, CancellationToken ct = default)
    {
        var total = await _db.WishlistSources
            .Where(w => w.CardPrintingId == cardPrintingId)
            .SumAsync(w => w.Quantity, ct);

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.CardPrintingId == cardPrintingId, ct);

        if (total <= 0)
        {
            if (uc is not null)
            {
                uc.QuantityWanted = 0;
                if (uc.QuantityOwned == 0 && uc.QuantityProxyOwned == 0)
                {
                    _db.UserCards.Remove(uc);
                    uc = null;
                }
                await _db.SaveChangesAsync(ct);
            }
            return uc;
        }

        if (uc is null)
        {
            uc = new UserCard
            {
                CardPrintingId = cardPrintingId,
                QuantityOwned = 0,
                QuantityProxyOwned = 0,
                QuantityWanted = total
            };
            _db.UserCards.Add(uc);
        }
        else
        {
            uc.QuantityWanted = total;
        }

        await _db.SaveChangesAsync(ct);
        return uc;
    }
}
