using api.Common.Errors;
using api.Data;
using api.Features.Values.Dtos;
using api.Features._Common;
using api.Models;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Values;

/// <summary>Provides collection and deck valuation endpoints.</summary>
[ApiController]
[Route("api/values")]
[Route("api/value")]
public sealed class ValuesController(
    AppDbContext db,
    IMapper mapper,
    TimeProvider timeProvider) : ControllerBase
{
    private const int UserId = DbSeeder.DefaultUserId;

    /// <summary>Records refreshed price snapshots for valid card printings in a game.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromQuery] string? game,
        [FromBody] List<RefreshItemRequest>? items,
        CancellationToken ct = default)
    {
        var validationError = ValidateRefreshRequest(game, items);
        if (validationError is not null)
            return validationError;

        var normalizedGame = SqliteCaseNormalizer.Normalize(game!);
        var gameId = await db.Games
            .AsNoTracking()
            .Where(candidate => candidate.NameNormalized == normalizedGame)
            .Select(candidate => (int?)candidate.Id)
            .FirstOrDefaultAsync(ct);

        var cpIds = items!.Select(i => i.CardPrintingId).ToHashSet();
        var validSet = gameId.HasValue
            ? (await db.CardPrintings
                .AsNoTracking()
                .Where(cp => cpIds.Contains(cp.Id) && cp.Card.GameId == gameId.Value)
                .Select(cp => cp.Id)
                .ToListAsync(ct)).ToHashSet()
            : [];

        var now = timeProvider.GetUtcNow();
        foreach (var item in items!.Where(x => validSet.Contains(x.CardPrintingId)))
        {
            db.ValueHistories.Add(new ValueHistory
            {
                ScopeType = ValueScopeType.CardPrinting,
                ScopeId = item.CardPrintingId,
                PriceCents = item.PriceCents,
                AsOfUtc = now,
                Source = string.IsNullOrWhiteSpace(item.Source) ? "manual" : item.Source!
            });
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Gets historical value points for a card printing.</summary>
    [HttpGet("cardprinting/{id:int}")]
    public async Task<ActionResult<SeriesResponse>> GetCardPrintingSeries(int id, CancellationToken ct = default)
    {
        if (!await db.CardPrintings.AsNoTracking().AnyAsync(x => x.Id == id, ct))
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card printing with id {id} was not found.");

        var histories = await db.ValueHistories
            .AsNoTracking()
            .Where(v => v.ScopeType == ValueScopeType.CardPrinting && v.ScopeId == id)
            .OrderBy(v => AppDbContext.SqliteJulianDay(v.AsOfUtc))
            .ToListAsync(ct);

        return Ok(new SeriesResponse(id, mapper.Map<List<SeriesPointResponse>>(histories)));
    }

    /// <summary>Computes the aggregate collection summary across owned printings, optionally scoped to a single game.</summary>
    [HttpGet("collection/summary")]
    public async Task<ActionResult<CollectionSummaryResponse>> GetCollectionSummary(
        [FromQuery] string? game = null,
        CancellationToken ct = default)
    {
        var latest = await LatestPricesAsync(ValueScopeType.CardPrinting, ct);

        var query = db.UserCards
            .AsNoTracking()
            .Where(uc => uc.UserId == UserId && uc.QuantityOwned > 0);

        if (!string.IsNullOrWhiteSpace(game))
        {
            var normalizedGame = SqliteCaseNormalizer.Normalize(game);
            query = query.Where(uc => uc.CardPrinting.Card.Game.NameNormalized == normalizedGame);
        }

        var data = await query
            .Select(uc => new { Game = uc.CardPrinting.Card.Game.Name, PrintingId = uc.CardPrintingId, Qty = uc.QuantityOwned })
            .ToListAsync(ct);

        long total = 0;
        var perGame = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in data)
        {
            if (!latest.TryGetValue(row.PrintingId, out var price)) continue;
            var add = price * row.Qty;
            total += add;
            perGame[row.Game] = perGame.TryGetValue(row.Game, out var existing) ? existing + add : add;
        }

        return Ok(new CollectionSummaryResponse(total, perGame.Select(kv => new GameSliceResponse(kv.Key, kv.Value))));
    }

    /// <summary>Computes the total value of a deck from the latest printing prices.</summary>
    [HttpGet("deck/{deckId:int}")]
    public async Task<ActionResult<DeckSummaryResponse>> GetDeckValue(int deckId, CancellationToken ct = default)
    {
        if (!await db.Decks.AsNoTracking().AnyAsync(d => d.Id == deckId, ct))
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Deck with id {deckId} was not found.");

        var latest = await LatestPricesAsync(ValueScopeType.CardPrinting, ct);
        var rows = await db.DeckCards
            .AsNoTracking()
            .Where(dc => dc.DeckId == deckId && dc.QuantityInDeck > 0)
            .Select(dc => new { dc.CardPrintingId, dc.QuantityInDeck })
            .ToListAsync(ct);

        long total = 0;
        foreach (var r in rows)
        {
            if (latest.TryGetValue(r.CardPrintingId, out var price))
                total += price * r.QuantityInDeck;
        }

        return Ok(new DeckSummaryResponse(deckId, total));
    }

    private IActionResult? ValidateRefreshRequest(string? game, List<RefreshItemRequest>? items)
    {
        if (string.IsNullOrWhiteSpace(game))
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["game"] = ["The 'game' query parameter is required."]
                },
                detail: "The refresh request must specify a game.");
        if (items is null)
            return this.CreateValidationProblem("items", "The request body must include an array of items.");
        if (items.Count == 0)
            return this.CreateValidationProblem("items", "At least one item must be provided.");
        return null;
    }

    private async Task<Dictionary<int, long>> LatestPricesAsync(ValueScopeType scope, CancellationToken ct)
    {
        var rows = await db.ValueHistories
            .AsNoTracking()
            .Where(v => v.ScopeType == scope)
            .Select(v => new { v.ScopeId, v.AsOfUtc, v.PriceCents })
            .ToListAsync(ct);

        return rows
            .GroupBy(v => v.ScopeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.AsOfUtc).First().PriceCents);
    }
}
