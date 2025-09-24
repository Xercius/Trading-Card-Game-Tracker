using api.Data;
using api.Models;
using api.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Generic;

// READ DTOs
public record CardPrintingDto(
    int Id,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl
);
public record CardDto(
    int Id,
    string Game,
    string Name,
    string CardType,
    string? Description,
    List<CardPrintingDto> Printings
);

// WRITE DTOs
public record CreateCardPrintingDto(string Set, string Number, string Rarity, string Style, string? ImageUrl);
public record CreateCardDto(string Game, string Name, string CardType, string? Description, List<CreateCardPrintingDto> Printings);
public record UpdateCardPrintingDto(int? Id, string Set, string Number, string Rarity, string Style, string? ImageUrl);
public record UpdateCardDto(string Game, string Name, string CardType, string? Description, List<UpdateCardPrintingDto> Printings);

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CardController(AppDbContext db) => _db = db;

        // GET /api/card
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetAll()
        {
            var data = await _db.Cards
                .Select(c => new CardDto(
                    c.Id, c.Game, c.Name, c.CardType, c.Description,
                    c.Printings.Select(p => new CardPrintingDto(
                        p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl
                    )).ToList()
                ))
                .ToListAsync();
            return Ok(data);
        }

        // GET /api/card/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CardDto>> GetOne(int id)
        {
            var c = await _db.Cards
                .Where(x => x.Id == id)
                .Select(x => new CardDto(
                    x.Id, x.Game, x.Name, x.CardType, x.Description,
                    x.Printings.Select(p => new CardPrintingDto(
                        p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl
                    )).ToList()
                ))
                .FirstOrDefaultAsync();

            return c is null ? NotFound() : Ok(c);
        }

        // POST /api/card
        [HttpPost]
        [AdminGuard]
        public async Task<ActionResult<CardDto>> Create([FromBody] CreateCardDto dto)
        {
            if (dto is null) return BadRequest();

            var card = new Card
            {
                Game = dto.Game,
                Name = dto.Name,
                CardType = dto.CardType,
                Description = dto.Description,
                Printings = (dto.Printings ?? new()).Select(p => new CardPrinting
                {
                    Set = p.Set,
                    Number = p.Number,
                    Rarity = p.Rarity,
                    Style = p.Style,
                    ImageUrl = p.ImageUrl
                }).ToList()
            };

            _db.Cards.Add(card);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOne), new { id = card.Id }, new CardDto(
                card.Id, card.Game, card.Name, card.CardType, card.Description,
                card.Printings.Select(p => new CardPrintingDto(p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl)).ToList()
            ));
        }

        // PUT /api/card/{id}  (full update; replaces printings list)
        [HttpPut("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCardDto dto)
        {
            var card = await _db.Cards.Include(c => c.Printings).FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return NotFound();

            card.Game = dto.Game;
            card.Name = dto.Name;
            card.CardType = dto.CardType;
            card.Description = dto.Description;

            // Replace printings: update matching by Id, add new where Id is null, remove missing
            var incoming = dto.Printings ?? new();
            var byId = card.Printings.ToDictionary(p => p.Id);

            // mark all existing as unseen
            var seen = new HashSet<int>();

            foreach (var p in incoming)
            {
                if (p.Id is int pid && byId.TryGetValue(pid, out var existing))
                {
                    existing.Set = p.Set; existing.Number = p.Number; existing.Rarity = p.Rarity;
                    existing.Style = p.Style; existing.ImageUrl = p.ImageUrl;
                    seen.Add(pid);
                }
                else
                {
                    card.Printings.Add(new CardPrinting
                    {
                        Set = p.Set,
                        Number = p.Number,
                        Rarity = p.Rarity,
                        Style = p.Style,
                        ImageUrl = p.ImageUrl
                    });
                }
            }

            // delete any not seen
            var toRemove = card.Printings.Where(p => !seen.Contains(p.Id) && incoming.All(x => x.Id != p.Id)).ToList();
            foreach (var r in toRemove) _db.CardPrintings.Remove(r);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/card/{id}
        [HttpDelete("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Delete(int id)
        {
            var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return NotFound();

            _db.Cards.Remove(card);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PATCH /api/card/{id}
        // Supported fields: Game, Name, CardType, Description
        [HttpPatch("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Patch(int id, [FromBody] JsonElement updates)
        {
            var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return NotFound();

            // Apply only fields present in the payload
            if (updates.TryGetProperty("game", out var gameProp) && gameProp.ValueKind == JsonValueKind.String)
                card.Game = gameProp.GetString()!;

            if (updates.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                card.Name = nameProp.GetString()!;

            if (updates.TryGetProperty("cardType", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                card.CardType = typeProp.GetString()!;

            // Allows setting Description to null explicitly
            if (updates.TryGetProperty("description", out var descProp))
                card.Description = descProp.ValueKind == JsonValueKind.Null ? null :
                                   descProp.ValueKind == JsonValueKind.String ? descProp.GetString() : card.Description;

            await _db.SaveChangesAsync();
            return NoContent();
        }

    }
}
