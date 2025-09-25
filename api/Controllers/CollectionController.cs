using api.Data;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// READ DTOs
public record UserCardItemDto(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned,
    int CardId,
    string CardName,
    string Game,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl
);
// WRITE DTOs
public record UpsertUserCardDto(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned
);
public record SetQuantitiesDto(
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned
);
public record CollectionDeltaDto(
    int CardPrintingId,
    int DeltaOwned,
    int DeltaWanted,
    int DeltaProxyOwned
);

namespace api.Controllers
{
    [ApiController]
    [RequireUserHeader]
    [Route("api/user/{userId:int}/collection")]
    // TODO: Derive the current user ID from the auth context instead of relying on the userId route parameter.
    public class CollectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CollectionController(AppDbContext db) => _db = db;

        private bool UserMismatch(int userId)
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || (!me.IsAdmin && me.Id != userId);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserCardItemDto>>> GetAll(
            int userId,
            [FromQuery] string? game,
            [FromQuery] string? set,
            [FromQuery] string? rarity,
            [FromQuery] string? name,
            [FromQuery] int? cardPrintingId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

            var query = _db.UserCards
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.CardPrinting).ThenInclude(cp => cp.Card)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(game))
                query = query.Where(uc => uc.CardPrinting.Card.Game == game);
            if (!string.IsNullOrWhiteSpace(set))
                query = query.Where(uc => uc.CardPrinting.Set == set);
            if (!string.IsNullOrWhiteSpace(rarity))
                query = query.Where(uc => uc.CardPrinting.Rarity == rarity);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var loweredName = name.ToLower();
                query = query.Where(uc => uc.CardPrinting.Card.Name.ToLower().Contains(loweredName));
            }
            if (cardPrintingId.HasValue)
                query = query.Where(uc => uc.CardPrintingId == cardPrintingId.Value);

            var rows = await query
                .Select(uc => new UserCardItemDto(
                    uc.CardPrintingId,
                    uc.QuantityOwned,
                    uc.QuantityWanted,
                    uc.QuantityProxyOwned,
                    uc.CardPrinting.CardId,
                    uc.CardPrinting.Card.Name,
                    uc.CardPrinting.Card.Game,
                    uc.CardPrinting.Set,
                    uc.CardPrinting.Number,
                    uc.CardPrinting.Rarity,
                    uc.CardPrinting.Style,
                    uc.CardPrinting.ImageUrl
                ))
                .ToListAsync();

            return Ok(rows);
        }

        // Upsert one entry
        [HttpPost]
        public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertUserCardDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
            if (await _db.Users.FindAsync(userId) is null) return NotFound("User not found.");
            if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null) return NotFound("CardPrinting not found.");

            var existing = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

            if (existing is null)
            {
                _db.UserCards.Add(new UserCard
                {
                    UserId = userId,
                    CardPrintingId = dto.CardPrintingId,
                    QuantityOwned = Math.Max(0, dto.QuantityOwned),
                    QuantityWanted = Math.Max(0, dto.QuantityWanted),
                    // Issue 9: proxy quantities clamped ≥ 0
                    QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned)
                });
            }
            else
            {
                existing.QuantityOwned = Math.Max(0, dto.QuantityOwned);
                existing.QuantityWanted = Math.Max(0, dto.QuantityWanted);
                // Issue 9: proxy quantities clamped ≥ 0
                existing.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Set owned, wanted, and proxy quantities in one request
        [HttpPut("{cardPrintingId:int}")]
        public async Task<IActionResult> SetQuantities(int userId, int cardPrintingId, [FromBody] SetQuantitiesDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            uc.QuantityOwned = Math.Max(0, dto.QuantityOwned);
            uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
            // Issue 9: proxy quantities clamped ≥ 0
            uc.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Partial update
        [HttpPatch("{cardPrintingId:int}")]
        public async Task<IActionResult> PatchQuantities(int userId, int cardPrintingId, [FromBody] JsonElement updates)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            if (updates.TryGetProperty("quantityOwned", out var qo) && qo.TryGetInt32(out var owned))
                uc.QuantityOwned = Math.Max(0, owned);

            if (updates.TryGetProperty("quantityWanted", out var qw) && qw.TryGetInt32(out var wanted))
                uc.QuantityWanted = Math.Max(0, wanted);
            if (updates.TryGetProperty("quantityProxyOwned", out var qpo) && qpo.TryGetInt32(out var proxyOwned))
                // Issue 9: proxy quantities clamped ≥ 0
                uc.QuantityProxyOwned = Math.Max(0, proxyOwned);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Issue 7 delta add/remove endpoint
        [HttpPost("delta")]
        public async Task<IActionResult> ApplyDelta(int userId, [FromBody] IEnumerable<CollectionDeltaDto> deltas)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (deltas is null) return BadRequest("Deltas payload required.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

            var deltaList = deltas.ToList();
            if (deltaList.Any(d => d.CardPrintingId <= 0))
                return BadRequest("CardPrintingId must be positive.");

            if (deltaList.Count == 0)
                return NoContent();

            var printingIds = deltaList.Select(d => d.CardPrintingId).Distinct().ToList();
            var existingPrintings = await _db.CardPrintings
                .Where(cp => printingIds.Contains(cp.Id))
                .Select(cp => cp.Id)
                .ToListAsync();

            var missingPrintingId = printingIds.FirstOrDefault(id => !existingPrintings.Contains(id));
            if (missingPrintingId != 0)
                return NotFound($"CardPrinting {missingPrintingId} not found.");

            var userCards = await _db.UserCards
                .Where(uc => uc.UserId == userId && printingIds.Contains(uc.CardPrintingId))
                .ToDictionaryAsync(uc => uc.CardPrintingId);

            foreach (var delta in deltaList)
            {
                if (!userCards.TryGetValue(delta.CardPrintingId, out var userCard))
                {
                    userCard = new UserCard
                    {
                        UserId = userId,
                        CardPrintingId = delta.CardPrintingId,
                        QuantityOwned = 0,
                        QuantityWanted = 0,
                        QuantityProxyOwned = 0
                    };
                    _db.UserCards.Add(userCard);
                    userCards[delta.CardPrintingId] = userCard;
                }

                userCard.QuantityOwned = Math.Max(0, userCard.QuantityOwned + delta.DeltaOwned);
                userCard.QuantityWanted = Math.Max(0, userCard.QuantityWanted + delta.DeltaWanted);
                // Issue 9: proxy quantities clamped ≥ 0
                userCard.QuantityProxyOwned = Math.Max(0, userCard.QuantityProxyOwned + delta.DeltaProxyOwned);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{cardPrintingId:int}")]
        public async Task<IActionResult> Remove(int userId, int cardPrintingId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            _db.UserCards.Remove(uc);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
