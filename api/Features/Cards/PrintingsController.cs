using api.Data;                 // your DbContext namespace
using api.Models;              // Card, CardPrinting
using api.Features.Cards.Dtos;
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

        if (!string.IsNullOrWhiteSpace(qp.Game))
            query = query.Where(p => p.Card.Game == qp.Game);

        if (!string.IsNullOrWhiteSpace(qp.Set))
            query = query.Where(p => p.Set == qp.Set);

        if (!string.IsNullOrWhiteSpace(qp.Number))
        {
            var number = qp.Number.Trim();
            query = query.Where(p => p.Number == number);
        }

        if (!string.IsNullOrWhiteSpace(qp.Rarity))
            query = query.Where(p => p.Rarity == qp.Rarity);

        if (!string.IsNullOrWhiteSpace(qp.Style))
            query = query.Where(p => p.Style == qp.Style);

        if (!string.IsNullOrWhiteSpace(qp.Q))
        {
            var term = qp.Q.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Card.Name, $"%{term}%") ||
                EF.Functions.Like(p.Number, $"%{term}%"));
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