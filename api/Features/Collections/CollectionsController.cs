using api.Data;
using api.Features.Collections.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace api.Features.Collections;

[ApiController]
[RequireUserHeader]
[Route("api/user/{userId:int}/collection")]
// TODO: Eventually remove legacy userId routes after clients migrate to /api/collection.
public class CollectionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CollectionsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private bool UserMismatch(int userId)
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || (!me.IsAdmin && me.Id != userId);
    }

    private bool IsAdmin() => HttpContext.GetCurrentUser()?.IsAdmin == true;

    private static bool TryGetInt(JsonElement obj, string camelName, string pascalName, out int value)
    {
        value = 0;
        return (obj.TryGetProperty(camelName, out var camel) && camel.TryGetInt32(out value))
            || (obj.TryGetProperty(pascalName, out var pascal) && pascal.TryGetInt32(out value));
    }

    private static bool IsZero(UserCard card) =>
        card.QuantityOwned == 0 && card.QuantityWanted == 0 && card.QuantityProxyOwned == 0;

    private static int ClampNonNegative(long value) =>
        value < 0 ? 0 : (value > int.MaxValue ? int.MaxValue : (int)value);

    private bool TryResolveCurrentUserId(out int userId, out IActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null) { error = Forbid(); userId = 0; return false; }
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
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
            return IsAdmin() ? NotFound("User not found.") : Forbid();

        var query = _db.UserCards
            .Where(uc => uc.UserId == userId)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(game))
            query = query.Where(uc => uc.CardPrinting.Card.Game == game);
        if (!string.IsNullOrWhiteSpace(set))
            query = query.Where(uc => uc.CardPrinting.Set == set);
        if (!string.IsNullOrWhiteSpace(rarity))
            query = query.Where(uc => uc.CardPrinting.Rarity == rarity);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{name.Trim()}%";
            query = query.Where(uc => EF.Functions.Like(uc.CardPrinting.Card.Name, pattern));
        }
        if (cardPrintingId.HasValue)
            query = query.Where(uc => uc.CardPrintingId == cardPrintingId.Value);

        var rows = await query
            .ProjectTo<UserCardItemResponse>(_mapper.ConfigurationProvider)
            .ToListAsync();

        return Ok(rows);
    }

    private async Task<IActionResult> UpsertCore(int userId, UpsertUserCardRequest dto)
    {
        if (dto is null) return BadRequest("Body required.");
        if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
        if (await _db.Users.FindAsync(userId) is null)
            return IsAdmin() ? NotFound("User not found.") : Forbid();
        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null) return NotFound("CardPrinting not found.");

        var existing = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (existing is null)
        {
            var newCard = new UserCard
            {
                UserId = userId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = Math.Max(0, dto.QuantityOwned),
                QuantityWanted = Math.Max(0, dto.QuantityWanted),
                QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned)
            };

            // Optional: skip creating an all-zero row on first insert.
            if (IsZero(newCard)) return NoContent();

            _db.UserCards.Add(newCard);
        }
        else
        {
            existing.QuantityOwned = Math.Max(0, dto.QuantityOwned);
            existing.QuantityWanted = Math.Max(0, dto.QuantityWanted);
            existing.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);

            // IMPORTANT: do NOT delete when all quantities are zero.
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetQuantitiesCore(int userId, int cardPrintingId, SetUserCardQuantitiesRequest dto)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null) return NotFound();

        uc.QuantityOwned = Math.Max(0, dto.QuantityOwned);
        uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
        uc.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);

        // Do NOT remove row when zero.
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> PatchQuantitiesCore(int userId, int cardPrintingId, JsonElement updates)
    {
        if (updates.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null) return NotFound();

        if (TryGetInt(updates, "quantityOwned", "QuantityOwned", out var owned))
            uc.QuantityOwned = Math.Max(0, owned);

        if (TryGetInt(updates, "quantityWanted", "QuantityWanted", out var wanted))
            uc.QuantityWanted = Math.Max(0, wanted);

        if (TryGetInt(updates, "quantityProxyOwned", "QuantityProxyOwned", out var proxyOwned))
            uc.QuantityProxyOwned = Math.Max(0, proxyOwned);

        // Do NOT remove row when zero.
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> ApplyDeltaCore(int userId, IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (deltas is null) return BadRequest("Deltas payload required.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
            return IsAdmin() ? NotFound("User not found.") : Forbid();

        var deltaList = deltas.ToList();
        if (deltaList.Count == 0) return NoContent();
        if (deltaList.Any(d => d.CardPrintingId <= 0))
            return BadRequest("CardPrintingId must be positive.");

        using var tx = await _db.Database.BeginTransactionAsync();
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

            row.QuantityOwned = ClampNonNegative((long)row.QuantityOwned + d.DeltaOwned);
            row.QuantityWanted = ClampNonNegative((long)row.QuantityWanted + d.DeltaWanted);
            row.QuantityProxyOwned = ClampNonNegative((long)row.QuantityProxyOwned + d.DeltaProxyOwned);
        }

        // Do NOT remove rows when zero.
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
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
        if (UserMismatch(userId)) return Forbid();
        return await GetAllCore(userId, game, set, rarity, name, cardPrintingId);
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertUserCardRequest dto)
    {
        if (UserMismatch(userId)) return Forbid();
        if (dto is null) return BadRequest("Body required.");
        return await UpsertCore(userId, dto);
    }

    [HttpPut("{cardPrintingId:int}")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetQuantities(int userId, int cardPrintingId, [FromBody] SetUserCardQuantitiesRequest dto)
    {
        if (UserMismatch(userId)) return Forbid();
        if (dto is null) return BadRequest("Body required.");
        return await SetQuantitiesCore(userId, cardPrintingId, dto);
    }

    [HttpPatch("{cardPrintingId:int}")]
    public async Task<IActionResult> PatchQuantities(int userId, int cardPrintingId, [FromBody] JsonElement patch)
    {
        if (UserMismatch(userId)) return Forbid();
        return await PatchQuantitiesCore(userId, cardPrintingId, patch);
    }

    [HttpPost("delta")]
    [Consumes("application/json")]
    public async Task<IActionResult> ApplyDelta(int userId, [FromBody] IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (UserMismatch(userId)) return Forbid();
        if (deltas is null) return BadRequest("Deltas payload required.");
        return await ApplyDeltaCore(userId, deltas);
    }

    [HttpDelete("{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(int userId, int cardPrintingId)
    {
        if (UserMismatch(userId)) return Forbid();
        return await RemoveCore(userId, cardPrintingId);
    }

    // -----------------------------------------
    // Auth-derived alias routes (/api/collection)
    // -----------------------------------------

    [HttpGet("/api/collection")]
    [HttpGet("/api/collections")]
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
    [HttpPost("/api/collections")]
    [Consumes("application/json")]
    public async Task<IActionResult> UpsertForCurrent([FromBody] UpsertUserCardRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (dto is null) return BadRequest("Body required.");
        return await UpsertCore(uid, dto);
    }

    [HttpPut("/api/collection/{cardPrintingId:int}")]
    [HttpPut("/api/collections/{cardPrintingId:int}")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetQuantitiesForCurrent(int cardPrintingId, [FromBody] SetUserCardQuantitiesRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (dto is null) return BadRequest("Body required.");
        return await SetQuantitiesCore(uid, cardPrintingId, dto);
    }

    [HttpPatch("/api/collection/{cardPrintingId:int}")]
    [HttpPatch("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> PatchQuantitiesForCurrent(int cardPrintingId, [FromBody] JsonElement patch)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await PatchQuantitiesCore(uid, cardPrintingId, patch);
    }

    [HttpPost("/api/collection/delta")]
    [HttpPost("/api/collections/delta")]
    [Consumes("application/json")]
    public async Task<IActionResult> ApplyDeltaForCurrent([FromBody] IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (deltas is null) return BadRequest("Deltas payload required.");
        return await ApplyDeltaCore(uid, deltas);
    }

    [HttpDelete("/api/collection/{cardPrintingId:int}")]
    [HttpDelete("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> RemoveForCurrent(int cardPrintingId)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await RemoveCore(uid, cardPrintingId);
    }
}
