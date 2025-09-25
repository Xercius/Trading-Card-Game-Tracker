using api.Data;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// DTOs
public record WishlistItemDto(int CardPrintingId, int QuantityWanted, int CardId, string CardName, string Game, string Set, string Number, string Rarity, string Style, string? ImageUrl);
public record UpsertWishlistDto(int CardPrintingId, int QuantityWanted);
public record BulkWishlistDto(IEnumerable<UpsertWishlistDto> Items);
public record MoveToCollectionDto(int CardPrintingId, int Quantity, bool UseProxy = false);

namespace api.Controllers
{
    [ApiController]
    [RequireUserHeader]
    [Route("api/user/{userId:int}/wishlist")]
    public class WishlistController : ControllerBase
    {
        private readonly AppDbContext _db;
        public WishlistController(AppDbContext db) => _db = db;

        private bool UserMismatch(int userId)
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || (!me.IsAdmin && me.Id != userId);
        }

        // GET: list wishlist items for user
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WishlistItemDto>>> GetAll(int userId,
            [FromQuery] string? game, [FromQuery] string? set, [FromQuery] string? rarity, [FromQuery] string? name, [FromQuery] int? cardPrintingId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

            var rows = await _db.WishlistEntries
                .Where(w => w.UserId == userId)
                .Include(w => w.CardPrinting).ThenInclude(cp => cp.Card)
                .Select(w => new WishlistItemDto(
                    w.CardPrintingId,
                    w.QuantityWanted,
                    w.CardPrinting.CardId,
                    w.CardPrinting.Card.Name,
                    w.CardPrinting.Card.Game,
                    w.CardPrinting.Set,
                    w.CardPrinting.Number,
                    w.CardPrinting.Rarity,
                    w.CardPrinting.Style,
                    w.CardPrinting.ImageUrl
                ))
                .ToListAsync();

            // optional filters (same pattern as collection)
            if (!string.IsNullOrWhiteSpace(game))   rows = rows.Where(r => r.Game   == game).ToList();
            if (!string.IsNullOrWhiteSpace(set))    rows = rows.Where(r => r.Set    == set).ToList();
            if (!string.IsNullOrWhiteSpace(rarity)) rows = rows.Where(r => r.Rarity == rarity).ToList();
            if (!string.IsNullOrWhiteSpace(name))   rows = rows.Where(r => r.CardName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (cardPrintingId.HasValue)            rows = rows.Where(r => r.CardPrintingId == cardPrintingId.Value).ToList();

            return Ok(rows);
        }

        // POST: upsert one wishlist entry (sets QuantityWanted)
        [HttpPost]
        public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertWishlistDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
            if (await _db.Users.FindAsync(userId) is null) return NotFound("User not found.");
            if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null) return NotFound("CardPrinting not found.");

            var existing = await _db.WishlistEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

            var clamped = Math.Max(0, dto.QuantityWanted);

            if (existing is null)
            {
                _db.WishlistEntries.Add(new WishlistEntry
                {
                    UserId = userId,
                    CardPrintingId = dto.CardPrintingId,
                    QuantityWanted = clamped
                });
            }
            else
            {
                existing.QuantityWanted = clamped;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT: bulk set quantities
        [HttpPut]
        public async Task<IActionResult> BulkSet(int userId, [FromBody] BulkWishlistDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (await _db.Users.FindAsync(userId) is null) return NotFound("User not found.");

            var cpIds = dto.Items.Select(i => i.CardPrintingId).ToHashSet();
            var validCp = await _db.CardPrintings.Where(cp => cpIds.Contains(cp.Id)).Select(cp => cp.Id).ToListAsync();
            var validSet = validCp.ToHashSet();

            var existing = await _db.WishlistEntries
                .Where(x => x.UserId == userId && cpIds.Contains(x.CardPrintingId))
                .ToListAsync();

            // upsert each
            foreach (var item in dto.Items)
            {
                if (item.CardPrintingId <= 0 || !validSet.Contains(item.CardPrintingId)) continue;
                var row = existing.FirstOrDefault(e => e.CardPrintingId == item.CardPrintingId);
                var clamped = Math.Max(0, item.QuantityWanted);

                if (row is null)
                {
                    _db.WishlistEntries.Add(new WishlistEntry
                    {
                        UserId = userId,
                        CardPrintingId = item.CardPrintingId,
                        QuantityWanted = clamped
                    });
                }
                else
                {
                    row.QuantityWanted = clamped;
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // POST: move from wishlist to collection
        [HttpPost("move-to-collection")]
        public async Task<IActionResult> MoveToCollection(int userId, [FromBody] MoveToCollectionDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
            if (dto.Quantity <= 0) return BadRequest("Quantity must be positive.");
            if (await _db.Users.FindAsync(userId) is null) return NotFound("User not found.");
            if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null) return NotFound("CardPrinting not found.");

            // decrement wishlist
            var wish = await _db.WishlistEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

            if (wish is null || wish.QuantityWanted <= 0)
                return BadRequest("No wishlist quantity to move.");

            var moveQty = Math.Min(dto.Quantity, wish.QuantityWanted);
            wish.QuantityWanted = Math.Max(0, wish.QuantityWanted - moveQty);

            // increment collection (owned or proxy)
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

            if (uc is null)
            {
                _db.UserCards.Add(new UserCard
                {
                    UserId = userId,
                    CardPrintingId = dto.CardPrintingId,
                    QuantityOwned = dto.UseProxy ? 0 : moveQty,
                    QuantityWanted = 0,
                    QuantityProxyOwned = dto.UseProxy ? moveQty : 0
                });
            }
            else
            {
                if (dto.UseProxy) uc.QuantityProxyOwned = Math.Max(0, uc.QuantityProxyOwned + moveQty);
                else              uc.QuantityOwned      = Math.Max(0, uc.QuantityOwned + moveQty);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
