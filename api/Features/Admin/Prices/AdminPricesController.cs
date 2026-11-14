using api.Data;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin.Prices;

/// <summary>
/// Admin-only endpoint for ingesting card price snapshots.
/// </summary>
[ApiController]
[Route("api/admin/prices")]
[Authorize(AuthenticationSchemes = "AdminToken", Roles = "Admin")]
public sealed class AdminPricesController(AdminPricesService service) : ControllerBase
{
    /// <summary>Ingests a batch of card price snapshots.</summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(
        [FromBody] IReadOnlyList<CardPriceSnapshotRequest>? payload,
        CancellationToken cancellationToken)
    {
        await service.IngestAsync(payload, cancellationToken);
        return NoContent();
    }
}

/// <summary>Handles validation and persistence for card price snapshot ingestion.</summary>
public sealed class AdminPricesService(AppDbContext db)
{
    /// <summary>Upserts valid card price snapshots into <see cref="CardPriceHistory"/>.</summary>
    public async Task IngestAsync(
        IReadOnlyList<CardPriceSnapshotRequest>? payload,
        CancellationToken cancellationToken)
    {
        if (payload is null || payload.Count == 0)
        {
            return;
        }

        var normalized = payload
            .Where(p => p.CardPrintingId > 0 && p.Price >= 0)
            .GroupBy(p => new { p.CardPrintingId, p.CapturedAt })
            .Select(g => g.Last())
            .ToList();

        if (normalized.Count == 0)
        {
            return;
        }

        var printingIds = normalized.Select(p => p.CardPrintingId).Distinct().ToList();
        var validSet = (await db.CardPrintings
            .AsNoTracking()
            .Where(cp => printingIds.Contains(cp.Id))
            .Select(cp => cp.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var filtered = normalized
            .Where(p => validSet.Contains(p.CardPrintingId))
            .Select(p => new CardPriceSnapshotRequest(
                p.CardPrintingId,
                p.CapturedAt,
                decimal.Round(p.Price, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        if (filtered.Count == 0)
        {
            return;
        }

        await UpsertSnapshotsAsync(filtered, cancellationToken);
    }

    private async Task UpsertSnapshotsAsync(
        IReadOnlyList<CardPriceSnapshotRequest> filtered,
        CancellationToken cancellationToken)
    {
        var printingIds = filtered.Select(p => p.CardPrintingId).Distinct().ToList();
        var dates = filtered.Select(p => p.CapturedAt).Distinct().ToList();

        var existing = await db.CardPriceHistories
            .Where(p => printingIds.Contains(p.CardPrintingId) && dates.Contains(p.CapturedAt))
            .ToDictionaryAsync(p => (p.CardPrintingId, p.CapturedAt), cancellationToken);

        foreach (var snapshot in filtered)
        {
            var key = (snapshot.CardPrintingId, snapshot.CapturedAt);
            if (existing.TryGetValue(key, out var entity))
            {
                if (entity.Price != snapshot.Price)
                {
                    entity.Price = snapshot.Price;
                }
                continue;
            }

            db.CardPriceHistories.Add(new CardPriceHistory
            {
                CardPrintingId = snapshot.CardPrintingId,
                CapturedAt = snapshot.CapturedAt,
                Price = snapshot.Price
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Request DTO for a single card price snapshot.</summary>
public sealed record CardPriceSnapshotRequest(int CardPrintingId, DateOnly CapturedAt, decimal Price);
