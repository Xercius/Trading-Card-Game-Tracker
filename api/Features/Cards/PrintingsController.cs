using api.Data;                 // your DbContext namespace
using api.Models;              // Card, CardPrinting
using api.Features.Cards.Dtos;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace api.Features.Cards;

[ApiController]
[Route("api/cards/printings")]
public sealed class PrintingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PrintingsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrintingDto>>> Get([FromQuery] ListPrintingsQuery qp, CancellationToken ct)
    {
        var query = _db.Set<CardPrinting>()
            .AsNoTracking()
            .Include(p => p.Card) // need Name + Game
            .AsQueryable();

        var games = CsvUtils.Parse(qp.Game);
        if (games.Count > 0)
        {
            query = query.Where(p =>
                p.Card.Game != null &&
                games.Contains(EF.Functions.Collate(p.Card.Game, "NOCASE")));
        }

        var sets = CsvUtils.Parse(qp.Set);
        if (sets.Count > 0)
        {
            query = query.Where(p =>
                p.Set != null &&
                sets.Any(s => EF.Functions.Collate(p.Set, "NOCASE") == s));
        }

        if (!string.IsNullOrWhiteSpace(qp.Number))
        {
            var number = qp.Number.Trim();
            query = query.Where(p => p.Number == number);
        }

        var rarities = CsvUtils.Parse(qp.Rarity);
        if (rarities.Count > 0)
        {
            query = query.Where(p =>
                p.Rarity != null &&
                rarities.Any(r => EF.Functions.Collate(p.Rarity, "NOCASE") == r));
        }

        var styles = CsvUtils.Parse(qp.Style);
        if (styles.Count > 0)
        {
            query = query.Where(p =>
                p.Style != null &&
                styles.Any(st => EF.Functions.Collate(p.Style, "NOCASE") == st));
        }

        if (!string.IsNullOrWhiteSpace(qp.Q))
        {
            var term = qp.Q.Trim();
            var pattern = $"%{term}%";
            query = query.Where(p =>
                (p.Card.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Card.Name, "NOCASE"), pattern)) ||
                (p.Number != null && EF.Functions.Like(EF.Functions.Collate(p.Number, "NOCASE"), pattern)) ||
                (p.Set != null && EF.Functions.Like(EF.Functions.Collate(p.Set, "NOCASE"), pattern)));
        }

        // simple sort (by set, then number, then name)
        query = query
            .OrderBy(p => p.Set)
            .ThenBy(p => p.Number)
            .ThenBy(p => p.Card.Name);

        // paging (optional)
        var page = Math.Max(1, qp.Page);
        var size = Math.Clamp(qp.PageSize, 1, 500);
        query = query.Skip((page - 1) * size).Take(size);

        var rows = await query
            .Select(p => new PrintingDto(
                p.Id,
                p.Set,
                null,
                p.Number,
                p.Rarity,
                (p.ImageUrl == null || p.ImageUrl.Trim() == "")
                    ? "/images/placeholders/card-3x4.png"
                    : p.ImageUrl,
                p.CardId,
                p.Card.Name,
                p.Card.Game
            ))
            .ToListAsync(ct);

        return Ok(rows);
    }
}