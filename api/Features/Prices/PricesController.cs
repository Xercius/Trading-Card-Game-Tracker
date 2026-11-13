using api.Common.Errors;
using api.Data;
using api.Features.Prices.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace api.Features.Prices;

/// <summary>Provides price history and value timeline endpoints.</summary>
[ApiController]
[Route("api/prices")]
public sealed class PricesController(AppDbContext db, TimeProvider timeProvider) : ControllerBase
{
    private const int DefaultHistoryDays = 30;
    private const int DefaultValueHistoryDays = 90;
    private const int UserId = DbSeeder.DefaultUserId;

    /// <summary>Gets price history for a single card printing.</summary>
    [HttpGet("{printingId:int}/history")]
    public async Task<ActionResult<PriceHistoryResponse>> GetHistory(
        int printingId,
        [FromQuery] int days = DefaultHistoryDays,
        CancellationToken ct = default)
    {
        if (printingId <= 0)
            return this.CreateValidationProblem("printingId", "printingId must be positive.");

        var exists = await db.CardPrintings.AsNoTracking().AnyAsync(cp => cp.Id == printingId, ct);
        if (!exists)
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: "Card printing not found.");

        var (rangeStart, _) = GetDateRange(days, DefaultHistoryDays);
        var rawPoints = await db.CardPriceHistories
            .AsNoTracking()
            .Where(v => v.CardPrintingId == printingId && v.CapturedAt >= rangeStart)
            .OrderBy(v => v.CapturedAt)
            .ToListAsync(ct);

        var grouped = rawPoints
            .GroupBy(v => v.CapturedAt)
            .Select(g => g.OrderByDescending(v => v.Id).First())
            .OrderBy(p => p.CapturedAt)
            .Select(p => new PricePointDto(
                p.CapturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Math.Round(p.Price, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return Ok(new PriceHistoryResponse(grouped));
    }

    /// <summary>Gets average daily price sparkline for a card across non-proxy printings.</summary>
    [HttpGet("/api/cards/{cardId:int}/sparkline")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetCardSparkline(
        int cardId,
        [FromQuery] int days = DefaultHistoryDays,
        CancellationToken ct = default)
    {
        if (cardId <= 0)
            return this.CreateValidationProblem("cardId", "cardId must be positive.");

        if (!await db.Cards.AsNoTracking().AnyAsync(c => c.Id == cardId, ct))
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: "Card not found.");

        var (rangeStart, today) = GetDateRange(days, DefaultHistoryDays);
        var printingIds = await db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .Where(cp => !EF.Functions.Like(cp.Treatment == null ? string.Empty : cp.Treatment.Name, "%proxy%"))
            .Select(cp => cp.Id)
            .ToListAsync(ct);

        if (printingIds.Count == 0)
            return Ok(Array.Empty<DailyValuePointDto>());

        var priceRows = await db.CardPriceHistories
            .AsNoTracking()
            .Where(p => printingIds.Contains(p.CardPrintingId) && p.CapturedAt >= rangeStart)
            .Select(p => new { p.CapturedAt, p.Price })
            .ToListAsync(ct);

        var perDay = priceRows.GroupBy(r => r.CapturedAt).ToDictionary(g => g.Key, g => g.Average(x => x.Price));
        return Ok(BuildTimeline(rangeStart, today, perDay));
    }

    /// <summary>Gets the daily total value history of the user's non-proxy collection.</summary>
    [HttpGet("/api/collection/value/history")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetCollectionValueHistory(
        [FromQuery] int days = DefaultValueHistoryDays,
        CancellationToken ct = default)
    {
        var (rangeStart, today) = GetDateRange(days, DefaultValueHistoryDays);
        var quantityMap = await db.UserCards
            .AsNoTracking()
            .Where(uc => uc.UserId == UserId && uc.QuantityOwned > 0)
            .Where(uc => !EF.Functions.Like(uc.CardPrinting.Treatment == null ? string.Empty : uc.CardPrinting.Treatment.Name, "%proxy%"))
            .ToDictionaryAsync(uc => uc.CardPrintingId, uc => uc.QuantityOwned, ct);

        return Ok(await ComputeWeightedTimelineAsync(quantityMap, rangeStart, today, ct));
    }

    /// <summary>Gets the daily total value history of a deck's non-proxy cards.</summary>
    [HttpGet("/api/decks/{deckId:int}/value/history")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetDeckValueHistory(
        int deckId,
        [FromQuery] int days = DefaultValueHistoryDays,
        CancellationToken ct = default)
    {
        if (deckId <= 0)
            return this.CreateValidationProblem("deckId", "deckId must be positive.");

        if (!await db.Decks.AsNoTracking().AnyAsync(d => d.Id == deckId, ct))
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: "Deck not found.");

        var (rangeStart, today) = GetDateRange(days, DefaultValueHistoryDays);
        var quantityMap = await db.DeckCards
            .AsNoTracking()
            .Where(dc => dc.DeckId == deckId && dc.QuantityInDeck > 0)
            .Where(dc => dc.CardPrinting != null && !EF.Functions.Like(
                dc.CardPrinting.Treatment == null ? string.Empty : dc.CardPrinting.Treatment.Name, "%proxy%"))
            .ToDictionaryAsync(dc => dc.CardPrintingId, dc => dc.QuantityInDeck, ct);

        return Ok(await ComputeWeightedTimelineAsync(quantityMap, rangeStart, today, ct));
    }

    private (DateOnly RangeStart, DateOnly Today) GetDateRange(int days, int defaultDays)
    {
        if (days <= 0) days = defaultDays;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return (today.AddDays(-(days - 1)), today);
    }

    private async Task<IReadOnlyList<DailyValuePointDto>> ComputeWeightedTimelineAsync(
        IReadOnlyDictionary<int, int> quantityMap,
        DateOnly rangeStart,
        DateOnly today,
        CancellationToken ct)
    {
        if (quantityMap.Count == 0)
            return Array.Empty<DailyValuePointDto>();

        var printingIds = quantityMap.Keys.ToList();
        var priceRows = await db.CardPriceHistories
            .AsNoTracking()
            .Where(p => printingIds.Contains(p.CardPrintingId) && p.CapturedAt >= rangeStart)
            .Select(p => new { p.CardPrintingId, p.CapturedAt, p.Price })
            .ToListAsync(ct);

        var totals = new Dictionary<DateOnly, decimal>();
        foreach (var row in priceRows)
        {
            if (!quantityMap.TryGetValue(row.CardPrintingId, out var qty) || qty <= 0)
                continue;

            totals[row.CapturedAt] = totals.GetValueOrDefault(row.CapturedAt) + (row.Price * qty);
        }

        return BuildTimeline(rangeStart, today, totals);
    }

    private static IReadOnlyList<DailyValuePointDto> BuildTimeline(
        DateOnly requestedStart,
        DateOnly today,
        IReadOnlyDictionary<DateOnly, decimal> values)
    {
        if (values.Count == 0 || requestedStart > today)
            return Array.Empty<DailyValuePointDto>();

        var firstData = values.Keys.Min();
        var start = firstData > requestedStart ? firstData : requestedStart;

        var results = new List<DailyValuePointDto>();
        for (var day = start; day <= today; day = day.AddDays(1))
        {
            var hasValue = values.TryGetValue(day, out var value);
            results.Add(new DailyValuePointDto(
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                hasValue ? Math.Round(value, 2, MidpointRounding.AwayFromZero) : null));
        }

        return results;
    }
}
