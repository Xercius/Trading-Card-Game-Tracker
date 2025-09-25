using api.Data;
using api.Filters;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public record DeckDto(
    int Id,
    int UserId,
    string Game,
    string Name,
    string? Description);
public record CreateDeckDto(
    string Game,
    string Name,
    string? Description
);
public record UpdateDeckDto(
    string Game,
    string Name,
    string? Description
);
public record DeckCardItemDto(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy,
    string CardName,
    string Set,
    string Number,
    string Rarity,
    string Style
);
public record UpsertDeckCardDto(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy
);
public record SetDeckCardQuantitiesDto(
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy
);

namespace api.Controllers
{
    // Issue 10: user-scoped deck CRUD tightening (ownership checks + optional filters)
    [ApiController]
    public class DeckController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DeckController(AppDbContext db) => _db = db;
        private bool UserMismatch(int userId)
        => HttpContext.GetCurrentUser() is { Id: var cid } && cid != userId;

        private bool NotOwnerAndNotAdmin(Deck d)
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || (!me.IsAdmin && me.Id != d.UserId);
        }

        // ----- User's decks ---------------------------------------------------

        // Issue 10
        // GET /api/user/{userId}/deck
        [HttpGet("api/user/{userId:int}/deck")]
        [RequireUserHeader]
        public async Task<ActionResult<IEnumerable<DeckDto>>> GetUserDecks(int userId, [FromQuery] string? game = null, [FromQuery] string? name = null, [FromQuery] bool? hasCards = null)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");
            var decks = await _db.Decks.Where(d => d.UserId == userId)
                .Select(d => new DeckDto(
                    d.Id,
                    d.UserId,
                    d.Game,
                    d.Name,
                    d.Description
                )
            )
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(game))
            {
                var trimmedGame = game.Trim();
                decks = decks.Where(d => d.Game == trimmedGame).ToList();
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                var trimmedName = name.Trim();
                decks = decks.Where(d => d.Name.Contains(trimmedName, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (hasCards.HasValue && hasCards.Value)
            {
                var deckIds = decks.Select(x => x.Id).ToList();
                var counts = await _db.DeckCards.Where(dc => deckIds.Contains(dc.DeckId))
                                            .GroupBy(dc => dc.DeckId)
                                            .Select(g => new { DeckId = g.Key, C = g.Count() })
                                            .ToListAsync();
                var withCards = counts.Select(x => x.DeckId).ToHashSet();
                decks = decks.Where(d => withCards.Contains(d.Id)).ToList();
            }
            return Ok(decks);
        }

        // Issue 10
        // POST /api/user/{userId}/deck
        [HttpPost("api/user/{userId:int}/deck")]
        [RequireUserHeader]
        public async Task<ActionResult<DeckDto>> CreateDeck(int userId, [FromBody] CreateDeckDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");
            if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Game and Name required.");

            var name = dto.Name.Trim();
            var game = dto.Game.Trim();
            // Optional: Prevent duplicate deck names per user.
            if (await _db.Decks.AnyAsync(x => x.UserId == userId && x.Name == name))
                return Conflict("A deck with this name already exists for this user.");

            var deck = new Deck {
                UserId = userId,
                Game = game,
                Name = name,
                Description = dto.Description
            };
            _db.Decks.Add(deck);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDeck),
                new { deckId = deck.Id },
                new DeckDto(
                    deck.Id,
                    deck.UserId,
                    deck.Game,
                    deck.Name,
                    deck.Description
                )
            );
        }

        // ----- Deck metadata --------------------------------------------------

        // Issue 10
        // GET /api/deck/{deckId}
        [HttpGet("api/deck/{deckId:int}")]
        [RequireUserHeader]
        public async Task<ActionResult<DeckDto>> GetDeck(int deckId)
        {
            var d = await _db.Decks.FindAsync(deckId);
            if (d is null) return NotFound();
            if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
            return Ok(new DeckDto(d.Id, d.UserId, d.Game, d.Name, d.Description));
        }

        // Issue 10
        // PATCH /api/deck/{deckId}
        [HttpPatch("api/deck/{deckId:int}")]
        [RequireUserHeader]
        public async Task<IActionResult> PatchDeck(int deckId, [FromBody] JsonElement updates)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();
            if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");

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

        // Issue 10
        // PUT /api/deck/{deckId}
        [HttpPut("api/deck/{deckId:int}")]
        [RequireUserHeader]
        public async Task<IActionResult> UpdateDeck(int deckId, [FromBody] UpdateDeckDto dto)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();
            if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
            if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Game and Name required.");

            d.Game = dto.Game.Trim();
            d.Name = dto.Name.Trim();
            d.Description = dto.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Issue 10
        // DELETE /api/deck/{deckId}
        [HttpDelete("api/deck/{deckId:int}")]
        [RequireUserHeader]
        public async Task<IActionResult> DeleteDeck(int deckId)
        {
            var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
            if (d is null) return NotFound();
            if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
            _db.Decks.Remove(d);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ----- Deck cards -----------------------------------------------------

        // GET /api/deck/{deckId}/cards
        [HttpGet("api/deck/{deckId:int}/cards")]
        [RequireUserHeader]
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
                    dc.QuantityProxy,
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
        [RequireUserHeader]
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
                    QuantityAcquire = Math.Max(0, dto.QuantityAcquire),
                    QuantityProxy = Math.Max(0, dto.QuantityProxy)
                });
            }
            else
            {
                existing.QuantityInDeck = Math.Max(0, dto.QuantityInDeck);
                existing.QuantityIdea = Math.Max(0, dto.QuantityIdea);
                existing.QuantityAcquire = Math.Max(0, dto.QuantityAcquire);
                existing.QuantityProxy = Math.Max(0, dto.QuantityProxy);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/deck/{deckId}/cards/{cardPrintingId}  (set all three counts)
        [HttpPut("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        [RequireUserHeader]
        public async Task<IActionResult> SetDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] SetDeckCardQuantitiesDto dto)
        {
            var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
            if (dc is null) return NotFound();

            dc.QuantityInDeck = Math.Max(0, dto.QuantityInDeck);
            dc.QuantityIdea = Math.Max(0, dto.QuantityIdea);
            dc.QuantityAcquire = Math.Max(0, dto.QuantityAcquire);
            dc.QuantityProxy = Math.Max(0, dto.QuantityProxy);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PATCH /api/deck/{deckId}/cards/{cardPrintingId}  (partial counts)
        [HttpPatch("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        [RequireUserHeader]
        public async Task<IActionResult> PatchDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] JsonElement updates)
        {
            var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
            if (dc is null) return NotFound();

            if (updates.TryGetProperty("quantityInDeck", out var qd) && qd.TryGetInt32(out var v1)) dc.QuantityInDeck = Math.Max(0, v1);
            if (updates.TryGetProperty("quantityIdea", out var qi) && qi.TryGetInt32(out var v2)) dc.QuantityIdea = Math.Max(0, v2);
            if (updates.TryGetProperty("quantityAcquire", out var qa) && qa.TryGetInt32(out var v3)) dc.QuantityAcquire = Math.Max(0, v3);
            if (updates.TryGetProperty("quantityProxy", out var qp) && qp.TryGetInt32(out var v4)) dc.QuantityProxy = Math.Max(0, v4);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/deck/{deckId}/cards/{cardPrintingId}
        [HttpDelete("api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
        [RequireUserHeader]
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
