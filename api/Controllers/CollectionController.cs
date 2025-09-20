using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

// READ DTOs
public record UserCardItemDto(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityWanted,
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
public record UpsertUserCardDto(int CardPrintingId, int QuantityOwned, int QuantityWanted);
public record SetQuantitiesDto(int QuantityOwned, int QuantityWanted);

namespace api.Controllers
{
    [ApiController]
    [Route("api/user/{userId:int}/collection")]
    public class CollectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CollectionController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserCardItemDto>>> GetAll(int userId)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

            var rows = await _db.UserCards
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.CardPrinting).ThenInclude(cp => cp.Card)
                .Select(uc => new UserCardItemDto(
                    uc.CardPrintingId,
                    uc.QuantityOwned,
                    uc.QuantityWanted,
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
                    QuantityWanted = Math.Max(0, dto.QuantityWanted)
                });
            }
            else
            {
                existing.QuantityOwned = Math.Max(0, dto.QuantityOwned);
                existing.QuantityWanted = Math.Max(0, dto.QuantityWanted);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Set both quantities
        [HttpPut("{cardPrintingId:int}")]
        public async Task<IActionResult> SetQuantities(int userId, int cardPrintingId, [FromBody] SetQuantitiesDto dto)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            uc.QuantityOwned = Math.Max(0, dto.QuantityOwned);
            uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Partial update
        [HttpPatch("{cardPrintingId:int}")]
        public async Task<IActionResult> PatchQuantities(int userId, int cardPrintingId, [FromBody] JsonElement updates)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            if (updates.TryGetProperty("quantityOwned", out var qo) && qo.TryGetInt32(out var owned))
                uc.QuantityOwned = Math.Max(0, owned);

            if (updates.TryGetProperty("quantityWanted", out var qw) && qw.TryGetInt32(out var wanted))
                uc.QuantityWanted = Math.Max(0, wanted);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{cardPrintingId:int}")]
        public async Task<IActionResult> Remove(int userId, int cardPrintingId)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            _db.UserCards.Remove(uc);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
