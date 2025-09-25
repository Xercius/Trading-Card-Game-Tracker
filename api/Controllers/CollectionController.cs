using api.Data;
using api.Middleware;
using api.Models;
using api.Filters;
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
public record DeltaUserCardDto(
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
    // TODO: Eventually remove legacy userId routes after clients migrate to /api/collection.
    public class CollectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CollectionController(AppDbContext db) => _db = db;

        // -----------------------------
        // Helpers
        // -----------------------------

        private bool UserMismatch(int userId)
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || (!me.IsAdmin && me.Id != userId);
        }

        private bool TryResolveCurrentUserId(out int userId, out IActionResult? error)
        {
            var me = HttpContext.GetCurrentUser();
            if (me is null) { error = StatusCode(403, "User missing."); userId = 0; return false; }
            error = null; userId = me.Id; return true;
        }

        // -----------------------------
        // Core (single source of logic)
        // -----------------------------

        private async Task<IActionResult> GetAllCore(
            int userId,
            string? game,
            string? set,
            string? rarity,
            string? name,
            int? cardPrintingId)
        {
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

        private async Task<IActionResult> UpsertCore(int userId, UpsertUserCardDto dto)
        {
            if (dto is null) return BadRequest();
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
                    QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned)
                });
            }
            else
            {
                existing.QuantityOwned = Math.Max(0, dto.QuantityOwned);
                existing.QuantityWanted = Math.Max(0, dto.QuantityWanted);
                existing.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<IActionResult> SetQuantitiesCore(int userId, int cardPrintingId, SetQuantitiesDto dto)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            uc.QuantityOwned = Math.Max(0, dto.QuantityOwned);
            uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
            uc.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<IActionResult> PatchQuantitiesCore(int userId, int cardPrintingId, JsonElement updates)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();

            if (updates.TryGetProperty("quantityOwned", out var qo) && qo.TryGetInt32(out var owned))
                uc.QuantityOwned = Math.Max(0, owned);

            if (updates.TryGetProperty("quantityWanted", out var qw) && qw.TryGetInt32(out var wanted))
                uc.QuantityWanted = Math.Max(0, wanted);

            if (updates.TryGetProperty("quantityProxyOwned", out var qpo) && qpo.TryGetInt32(out var proxyOwned))
                uc.QuantityProxyOwned = Math.Max(0, proxyOwned);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<IActionResult> ApplyDeltaCore(int userId, IEnumerable<DeltaUserCardDto> deltas)
        {
            if (deltas is null) return BadRequest("Deltas payload required.");
            if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

            var deltaList = deltas.ToList();
            if (deltaList.Count == 0) return NoContent();
            if (deltaList.Any(d => d.CardPrintingId <= 0))
                return BadRequest("CardPrintingId must be positive.");

            var printingIds = deltaList.Select(d => d.CardPrintingId).Distinct().ToList();
            var validIds = await _db.CardPrintings
                .Where(cp => printingIds.Contains(cp.Id))
                .Select(cp => cp.Id)
                .ToListAsync();

            var missing = printingIds.FirstOrDefault(id => !validIds.Contains(id));
            if (missing != 0) return NotFound($"CardPrinting not found: {missing}");

            var map = await _db.UserCards
                .Where(uc => uc.UserId == userId && printingIds.Contains(uc.CardPrintingId))
                .ToDictionaryAsync(uc => uc.CardPrintingId);

            foreach (var d in deltaList)
            {
                if (!map.TryGetValue(d.CardPrintingId, out var row))
                {
                    row = new UserCard
                    {
                        UserId = userId,
                        CardPrintingId = d.CardPrintingId,
                        QuantityOwned = 0,
                        QuantityWanted = 0,
                        QuantityProxyOwned = 0
                    };
                    _db.UserCards.Add(row);
                    map[d.CardPrintingId] = row;
                }

                row.QuantityOwned = Math.Max(0, row.QuantityOwned + d.DeltaOwned);
                row.QuantityWanted = Math.Max(0, row.QuantityWanted + d.DeltaWanted);
                row.QuantityProxyOwned = Math.Max(0, row.QuantityProxyOwned + d.DeltaProxyOwned);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<IActionResult> RemoveCore(int userId, int cardPrintingId)
        {
            var uc = await _db.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
            if (uc is null) return NotFound();
            _db.UserCards.Remove(uc);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // -----------------------------------------
        // Legacy routes (userId in the URL)
        // -----------------------------------------

        [HttpGet]
        public async Task<IActionResult> GetAll(
            int userId,
            [FromQuery] string? game,
            [FromQuery] string? set,
            [FromQuery] string? rarity,
            [FromQuery] string? name,
            [FromQuery] int? cardPrintingId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await GetAllCore(userId, game, set, rarity, name, cardPrintingId);
        }

        [HttpPost]
        public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertUserCardDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await UpsertCore(userId, dto);
        }

        [HttpPut("{cardPrintingId:int}")]
        public async Task<IActionResult> SetQuantities(int userId, int cardPrintingId, [FromBody] SetQuantitiesDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await SetQuantitiesCore(userId, cardPrintingId, dto);
        }

        [HttpPatch("{cardPrintingId:int}")]
        public async Task<IActionResult> PatchQuantities(int userId, int cardPrintingId, [FromBody] JsonElement updates)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await PatchQuantitiesCore(userId, cardPrintingId, updates);
        }

        [HttpPost("delta")]
        public async Task<IActionResult> ApplyDelta(int userId, [FromBody] IEnumerable<DeltaUserCardDto> deltas)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await ApplyDeltaCore(userId, deltas);
        }

        [HttpDelete("{cardPrintingId:int}")]
        public async Task<IActionResult> Remove(int userId, int cardPrintingId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await RemoveCore(userId, cardPrintingId);
        }

        // -----------------------------------------
        // Auth-derived alias routes (/api/collection)
        // -----------------------------------------

        [HttpGet("/api/collection")]
        public async Task<IActionResult> GetAllForCurrent(
            [FromQuery] string? game,
            [FromQuery] string? set,
            [FromQuery] string? rarity,
            [FromQuery] string? name,
            [FromQuery] int? cardPrintingId)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await GetAllCore(uid, game, set, rarity, name, cardPrintingId);
        }

        [HttpPost("/api/collection")]
        public async Task<IActionResult> UpsertForCurrent([FromBody] UpsertUserCardDto dto)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await UpsertCore(uid, dto);
        }

        [HttpPut("/api/collection/{cardPrintingId:int}")]
        public async Task<IActionResult> SetQuantitiesForCurrent(int cardPrintingId, [FromBody] SetQuantitiesDto dto)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await SetQuantitiesCore(uid, cardPrintingId, dto);
        }

        [HttpPatch("/api/collection/{cardPrintingId:int}")]
        public async Task<IActionResult> PatchQuantitiesForCurrent(int cardPrintingId, [FromBody] JsonElement updates)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await PatchQuantitiesCore(uid, cardPrintingId, updates);
        }

        [HttpPost("/api/collection/delta")]
        public async Task<IActionResult> ApplyDeltaForCurrent([FromBody] IEnumerable<DeltaUserCardDto> deltas)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await ApplyDeltaCore(uid, deltas);
        }

        [HttpDelete("/api/collection/{cardPrintingId:int}")]
        public async Task<IActionResult> RemoveForCurrent(int cardPrintingId)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await RemoveCore(uid, cardPrintingId);
        }
    }
}
