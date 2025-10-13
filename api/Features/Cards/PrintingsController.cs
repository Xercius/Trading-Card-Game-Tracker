using System.Linq;
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

        var games = CsvUtils.Parse(qp.Game)
            .Select(x => x.ToLower())
            .ToList();
        if (games.Count > 0)
            query = query.Where(p => p.Card.Game != null && games.Contains(p.Card.Game.ToLower()));

        var sets = CsvUtils.Parse(qp.Set)
            .Select(x => x.ToLower())
            .ToList();
        if (sets.Count > 0)
            query = query.Where(p => p.Set != null && sets.Contains(p.Set.ToLower()));

        if (!string.IsNullOrWhiteSpace(qp.Number))
        {
            var number = qp.Number.Trim();
            query = query.Where(p => p.Number == number);
        }

        var rarities = CsvUtils.Parse(qp.Rarity)
            .Select(x => x.ToLower())
            .ToList();
        if (rarities.Count > 0)
            query = query.Where(p => p.Rarity != null && rarities.Contains(p.Rarity.ToLower()));

        var styles = CsvUtils.Parse(qp.Style)
            .Select(x => x.ToLower())
            .ToList();
        if (styles.Count > 0)
            query = query.Where(p => p.Style != null && styles.Contains(p.Style.ToLower()));

        if (!string.IsNullOrWhiteSpace(qp.Q))
        {
            var term = qp.Q.Trim().ToLower();
            query = query.Where(p =>
                (p.Card.Name != null && p.Card.Name.ToLower().Contains(term)) ||
                (p.Number != null && p.Number.ToLower().Contains(term)) ||
                (p.Set != null && p.Set.ToLower().Contains(term)));
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