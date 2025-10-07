using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Data;
using api.Filters;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin.Prices;

[ApiController]
[RequireUserHeader]
[AdminGuard]
[Route("api/admin/prices")]
public sealed class AdminPricesController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IReadOnlyList<CardPriceSnapshotRequest>? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return NoContent();
        }

        var normalized = payload
            .Where(p => p.CardPrintingId > 0)
            .Where(p => p.Price >= 0)
            .GroupBy(p => new { p.CardPrintingId, p.CapturedAt })
            .Select(g => g.Last())
            .ToList();

        if (normalized.Count == 0)
        {
            return NoContent();
        }

        var printingIds = normalized.Select(p => p.CardPrintingId).Distinct().ToList();

        var validIds = await _db.CardPrintings
            .Where(cp => printingIds.Contains(cp.Id))
            .Select(cp => cp.Id)
            .ToListAsync();

        if (validIds.Count == 0)
        {
            return NoContent();
        }

        var validSet = validIds.ToHashSet();
        var filtered = normalized
            .Where(p => validSet.Contains(p.CardPrintingId))
            .Select(p => new CardPriceSnapshotRequest(
                p.CardPrintingId,
                p.CapturedAt,
                decimal.Round(p.Price, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        if (filtered.Count == 0)
        {
            return NoContent();
        }

        printingIds = filtered.Select(p => p.CardPrintingId).Distinct().ToList();
        var dates = filtered.Select(p => p.CapturedAt).Distinct().ToList();

        var existing = await _db.CardPriceHistories
            .Where(p => printingIds.Contains(p.CardPrintingId) && dates.Contains(p.CapturedAt))
            .ToDictionaryAsync(p => (p.CardPrintingId, p.CapturedAt));

        foreach (var snapshot in filtered)
        {
            var key = (snapshot.CardPrintingId, snapshot.CapturedAt);
            if (existing.TryGetValue(key, out var entity))
            {
                if (entity.Price != snapshot.Price)
                {
                    entity.Price = snapshot.Price;
                }
            }
            else
            {
                _db.CardPriceHistories.Add(new CardPriceHistory
                {
                    CardPrintingId = snapshot.CardPrintingId,
                    CapturedAt = snapshot.CapturedAt,
                    Price = snapshot.Price
                });
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public sealed record CardPriceSnapshotRequest(int CardPrintingId, DateOnly CapturedAt, decimal Price);
