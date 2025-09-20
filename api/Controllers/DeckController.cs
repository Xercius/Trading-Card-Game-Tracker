using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

public record DeckDto(int Id, int UserId, string Game, string Name, string? Description);
public record CreateDeckDto(string Game, string Name, string? Description);
public record UpdateDeckDto(string Game, string Name, string? Description);

public record DeckCardItemDto(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    string CardName,
    string Set,
    string Number,
    string Rarity,
    string Style
);
public record UpsertDeckCardDto(int CardPrintingId, int QuantityInDeck, int QuantityIdea, int QuantityAcquire);
public record SetDeckCardQuantitiesDto(int QuantityInDeck, int QuantityIdea, int QuantityAcquire);

namespace api.Controllers
{
    [ApiController]
    public class DeckController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DeckController(AppDbContext db) => _db = db;

        // ----- User's decks ---------------------------------------------------

        // GET /api/user/{userId}/deck
        [HttpGet("api/user/{userId:int}/deck")]
        public async Task<ActionResult<IEnumerable<DeckDto>>> GetUserDecks(int userId)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");
            var decks = await _db.Decks.Where(d => d.UserId == userId)
                .Select(d => new DeckDto(d.Id, d.UserId, d.Game, d.Name, d.Description))
                .ToListAsync();
            return Ok(decks);
        }

        // POST /api/user/{userId}/deck
        [HttpPost("api/user/{userId:int}/deck")]
        public async Task<ActionResult<DeckDto>> CreateDeck(int userId, [FromBody] CreateDeckDto dto)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");
            if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Game and Name required.");

            var deck = new Deck { UserId = userId, Game = dto.Game.Trim(), Name = dto.Name.Trim(), Description = dto.Description };
            _db.Decks.Add(deck);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDeck), new { deckId = deck.Id }, new DeckDto(deck.Id, deck.UserId, deck.Game, deck.Name, deck.Description));
        }

        // ----- Deck metadata --------------------------------------------------

        // GET /api/deck/{deckId}
        [HttpGet("api/deck/{deckId:int}")]
        public async Task<ActionResult<DeckDto>> GetDeck(int deckId)
        {
            var d = await _db.Decks.FindAsync(deckId);
            return d is null ? NotFound() : Ok(new DeckDto(d.Id, d.UserId, d.Game, d.Name, d.Description));
        }

        // PATCH /api/deck/{deckId}
        [HttpPatch("api/deck/{deckId:int}")]
        public async Task<IActionResult> PatchDeck(int deckId, [FromBody] JsonElement updates)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();

            if (updates.TryGetProperty("game", out var g) && g.ValueKind == JsonValueKind.String)
                d.Game = g.GetString()!.Trim();
            if (updates.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                d.Name = n.GetString()!.Trim();
            if (updates.TryGetProperty("description", out var desc))
                d.Description = desc.ValueKind == JsonValueKind.Null ? null :
                                desc.ValueKind == JsonValueKind.String ? desc.GetString() : d.Description;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/deck/{deckId}
        [HttpPut("api/deck/{deckId:int}")]
        public async Task<IActionResult> UpdateDeck(int deckId, [FromBody] UpdateDeckDto dto)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Game and Name required.");

            d.Game = dto.Game.Trim();
            d.Name = dto.Name.Trim();
            d.Description = dto.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/deck/{deckId}
        [HttpDelete("api/deck/{deckId:int}")]
        public async Task<IActionResult> DeleteDeck(int deckId)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();
            _db.Decks.Remove(d);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ----- Deck cards -----------------------------------------------------

        // GET /api/deck/{deckId}/cards
        [HttpGet("api/deck/{deckId:int}/cards")]
        public async Task<ActionResult<IEnumerable<DeckCardItemDto>>> GetDeckCards(int deckId)
        {
            var deck = await _db.Decks.FindAsync(deckId);
            if (deck is null) return NotFound();

            var items = await _db.DeckCards
                .Where(dc => dc.DeckId == deckId)
                .Include(dc => dc.CardPrinting).ThenInclude(cp => cp.Card)
                .Select(dc => new DeckCardItemDto(
                    dc.CardPrintingId,
                    dc.QuantityInDeck,
                    dc.QuantityIdea,
                    dc.QuantityAcquire,
                    dc.CardPrinting.Card.Name,
                    dc.CardPrinting.Set,
                    dc.CardPrinting.Number,
                    dc.CardPrinting.Rarity,
                    dc.CardPrinting.Style
                ))
                .ToListAsync();

            return Ok(items);
        }

        // POST /api/deck/{deckId}/cards  (upsert one printing)
        [HttpPost("api/deck/{deckId:int}/cards")]
        public async Task<IActionResult> UpsertDeckCard(int deckId, [FromBody] UpsertDeckCardDto dto)
        {
            var deck = await _db.Decks.FindAsync(deckId);
            if (deck is null) return NotFound("Deck not found.");

            var printing = await _db.CardPrintings.Include(cp => cp.Card).FirstOrDefaultAsync(cp => cp.Id == dto.CardPrintingId);
            if (printing is null) return NotFound("CardPrinting not found.");

            // Enforce same game
            if (!string.Equals(deck.Game, printing.Card.Game, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Card game does not match deck game.");

            var existing = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == dto.CardPrintingId);
            if (existing is null)
            {
                _db.DeckCards.Add(new DeckCard
                {
                    DeckId = deckId,
                    CardPrintingId = dto.CardPrintingId,
                    QuantityInDeck = Math.Max(0, dto.QuantityInDeck),
                    QuantityIdea = Math.Max(0, dto.QuantityIdea),
                    QuantityAcquire = Math.Max(0, dto.QuantityAcquire)
                });
            }
            else
            {
                existing.QuantityInDeck = Math.Max(0, dto.QuantityInDeck);
                existing.QuantityIdea = Math.Max(0, dto.QuantityIdea);
                existing.QuantityAcquire = Math.Max(0, dto.QuantityAcquire);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/deck/{deckId}/cards/{cardPrintingId}  (set all three counts)
        [HttpPut("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        public async Task<IActionResult> SetDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] SetDeckCardQuantitiesDto dto)
        {
            var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
            if (dc is null) return NotFound();

            dc.QuantityInDeck = Math.Max(0, dto.QuantityInDeck);
            dc.QuantityIdea = Math.Max(0, dto.QuantityIdea);
            dc.QuantityAcquire = Math.Max(0, dto.QuantityAcquire);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PATCH /api/deck/{deckId}/cards/{cardPrintingId}  (partial counts)
        [HttpPatch("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        public async Task<IActionResult> PatchDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] JsonElement updates)
        {
            var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
            if (dc is null) return NotFound();

            if (updates.TryGetProperty("quantityInDeck", out var qd) && qd.TryGetInt32(out var v1)) dc.QuantityInDeck = Math.Max(0, v1);
            if (updates.TryGetProperty("quantityIdea", out var qi) && qi.TryGetInt32(out var v2)) dc.QuantityIdea = Math.Max(0, v2);
            if (updates.TryGetProperty("quantityAcquire", out var qa) && qa.TryGetInt32(out var v3)) dc.QuantityAcquire = Math.Max(0, v3);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/deck/{deckId}/cards/{cardPrintingId}
        [HttpDelete("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        public async Task<IActionResult> RemoveDeckCard(int deckId, int cardPrintingId)
        {
            var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
            if (dc is null) return NotFound();
            _db.DeckCards.Remove(dc);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
