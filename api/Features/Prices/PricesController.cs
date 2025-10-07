using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using api.Data;
using api.Features.Prices.Dtos;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Prices;

[ApiController]
[Route("api/prices")]
[RequireUserHeader]
public sealed class PricesController(AppDbContext db) : ControllerBase
{
    private const int DefaultHistoryDays = 30;
    private const int DefaultValueHistoryDays = 90;
    private readonly AppDbContext _db = db;

    [HttpGet("{printingId:int}/history")]
    public async Task<ActionResult<PriceHistoryResponse>> GetHistory(
        int printingId,
        [FromQuery] int days = DefaultHistoryDays)
    {
        if (printingId <= 0) return BadRequest("printingId must be positive.");

        var exists = await _db.CardPrintings.AnyAsync(cp => cp.Id == printingId);
        if (!exists) return NotFound();

        if (days <= 0) days = DefaultHistoryDays;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var rangeStart = today.AddDays(-(days - 1));

        var rawPoints = await _db.CardPriceHistories
            .Where(v => v.CardPrintingId == printingId && v.CapturedAt >= rangeStart)
            .OrderBy(v => v.CapturedAt)
            .ToListAsync();

        var grouped = rawPoints
            .GroupBy(v => v.CapturedAt)
            .Select(g => g
                .OrderByDescending(v => v.Id)
                .First())
            .OrderBy(p => p.CapturedAt)
            .Select(p => new PricePointDto(
                p.CapturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Math.Round(p.Price, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return Ok(new PriceHistoryResponse(grouped));
    }

    [HttpGet("/api/cards/{cardId:int}/sparkline")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetCardSparkline(
        int cardId,
        [FromQuery] int days = DefaultHistoryDays)
    {
        if (cardId <= 0) return BadRequest("cardId must be positive.");

        var cardExists = await _db.Cards.AnyAsync(c => c.CardId == cardId);
        if (!cardExists) return NotFound();

        if (days <= 0) days = DefaultHistoryDays;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var rangeStart = today.AddDays(-(days - 1));

        var printingIds = await _db.CardPrintings
            .Where(cp => cp.CardId == cardId)
            .Where(cp => !EF.Functions.Like(cp.Style, "%proxy%"))
            .Select(cp => cp.Id)
            .ToListAsync();

        if (printingIds.Count == 0)
            return Ok(Array.Empty<DailyValuePointDto>());

        var priceRows = await _db.CardPriceHistories
            .Where(p => printingIds.Contains(p.CardPrintingId) && p.CapturedAt >= rangeStart)
            .Select(p => new { p.CapturedAt, p.Price })
            .ToListAsync();

        var perDay = priceRows
            .GroupBy(r => r.CapturedAt)
            .ToDictionary(
                g => g.Key,
                g => g.Average(x => x.Price));

        var timeline = BuildTimeline(rangeStart, today, perDay);
        return Ok(timeline);
    }

    [HttpGet("/api/collection/value/history")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetCollectionValueHistory(
        [FromQuery] int days = DefaultValueHistoryDays)
    {
        var user = HttpContext.GetCurrentUser();
        if (user is null) return BadRequest("X-User-Id header required.");

        if (days <= 0) days = DefaultValueHistoryDays;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var rangeStart = today.AddDays(-(days - 1));

        var ownedPrintings = await _db.UserCards
            .Where(uc => uc.UserId == user.Id && uc.QuantityOwned > 0)
            .Where(uc => !EF.Functions.Like(uc.CardPrinting.Style, "%proxy%"))
            .Select(uc => new { uc.CardPrintingId, uc.QuantityOwned })
            .ToListAsync();

        if (ownedPrintings.Count == 0)
            return Ok(Array.Empty<DailyValuePointDto>());

        var quantityMap = ownedPrintings.ToDictionary(x => x.CardPrintingId, x => x.QuantityOwned);
        var printingIds = quantityMap.Keys.ToList();

        var priceRows = await _db.CardPriceHistories
            .Where(p => printingIds.Contains(p.CardPrintingId) && p.CapturedAt >= rangeStart)
            .Select(p => new { p.CardPrintingId, p.CapturedAt, p.Price })
            .ToListAsync();

        var totals = new Dictionary<DateOnly, decimal>();
        foreach (var row in priceRows)
        {
            if (!quantityMap.TryGetValue(row.CardPrintingId, out var qty) || qty <= 0)
                continue;

            var value = row.Price * qty;
            if (totals.TryGetValue(row.CapturedAt, out var existing))
                totals[row.CapturedAt] = existing + value;
            else
                totals[row.CapturedAt] = value;
        }

        var timeline = BuildTimeline(rangeStart, today, totals);
        return Ok(timeline);
    }

    [HttpGet("/api/decks/{deckId:int}/value/history")]
    public async Task<ActionResult<IReadOnlyList<DailyValuePointDto>>> GetDeckValueHistory(
        int deckId,
        [FromQuery] int days = DefaultValueHistoryDays)
    {
        if (deckId <= 0) return BadRequest("deckId must be positive.");

        var deckExists = await _db.Decks.AnyAsync(d => d.Id == deckId);
        if (!deckExists) return NotFound();

        if (days <= 0) days = DefaultValueHistoryDays;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var rangeStart = today.AddDays(-(days - 1));

        var deckPrintings = await _db.DeckCards
            .Where(dc => dc.DeckId == deckId && dc.QuantityInDeck > 0)
            .Where(dc => !EF.Functions.Like(dc.CardPrinting.Style, "%proxy%"))
            .Select(dc => new { dc.CardPrintingId, dc.QuantityInDeck })
            .ToListAsync();

        if (deckPrintings.Count == 0)
            return Ok(Array.Empty<DailyValuePointDto>());

        var quantityMap = deckPrintings.ToDictionary(x => x.CardPrintingId, x => x.QuantityInDeck);
        var printingIds = quantityMap.Keys.ToList();

        var priceRows = await _db.CardPriceHistories
            .Where(p => printingIds.Contains(p.CardPrintingId) && p.CapturedAt >= rangeStart)
            .Select(p => new { p.CardPrintingId, p.CapturedAt, p.Price })
            .ToListAsync();

        var totals = new Dictionary<DateOnly, decimal>();
        foreach (var row in priceRows)
        {
            if (!quantityMap.TryGetValue(row.CardPrintingId, out var qty) || qty <= 0)
                continue;

            var value = row.Price * qty;
            if (totals.TryGetValue(row.CapturedAt, out var existing))
                totals[row.CapturedAt] = existing + value;
            else
                totals[row.CapturedAt] = value;
        }

        var timeline = BuildTimeline(rangeStart, today, totals);
        return Ok(timeline);
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
