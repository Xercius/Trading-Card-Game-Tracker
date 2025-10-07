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
    private readonly AppDbContext _db = db;

    [HttpGet("{printingId:int}/history")]
    public async Task<ActionResult<PriceHistoryResponse>> GetHistory(
        int printingId,
        [FromQuery] int days = 30)
    {
        if (printingId <= 0) return BadRequest("printingId must be positive.");

        var exists = await _db.CardPrintings.AnyAsync(cp => cp.Id == printingId);
        if (!exists) return NotFound();

        if (days <= 0) days = 30;
        var cutoffUtc = DateTime.UtcNow.AddDays(-days);

        var rawPoints = await _db.ValueHistories
            .Where(v => v.ScopeType == ValueScopeType.CardPrinting && v.ScopeId == printingId)
            .Where(v => v.AsOfUtc >= cutoffUtc)
            .Select(v => new
            {
                v.AsOfUtc,
                v.PriceCents
            })
            .ToListAsync();

        var grouped = rawPoints
            .GroupBy(v => DateOnly.FromDateTime(v.AsOfUtc))
            .OrderBy(g => g.Key)
            .Select(g => g
                .OrderByDescending(v => v.AsOfUtc)
                .First())
            .Select(p => new PricePointDto(
                p.AsOfUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                p.PriceCents.ToDollars()))
            .ToList();

        return Ok(new PriceHistoryResponse(grouped));
    }
}

public static class PriceExtensions
{
    public static decimal ToDollars(this int priceCents)
    {
        return Math.Round(priceCents / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
