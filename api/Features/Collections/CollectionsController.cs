using api.Common.Errors;
using api.Data;
using api.Features.Collections.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CollectionItemDto = api.Features.Collections.Dtos.UserCardItemResponse;

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

    private bool TryResolveCurrentUserId(out int userId, out ActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null) { error = Forbid(); userId = 0; return false; }
        error = null; userId = me.Id; return true;
    }

    // -----------------------------
    // Core (single source of logic)
    // -----------------------------

    private async Task<ActionResult<Paged<CollectionItemDto>>> GetAllCore(
        int userId,
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        int page,
        int pageSize)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            if (IsAdmin())
            {
                return this.CreateProblem(StatusCodes.Status404NotFound, detail: "User not found.");
            }

            return Forbid();
        }

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 50;

        var ct = HttpContext.RequestAborted;

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

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(uc => uc.CardPrinting.Card.Name)
            .ThenBy(uc => uc.CardPrintingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<CollectionItemDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(new Paged<CollectionItemDto>(items, total, page, pageSize));
    }

    private async Task<IActionResult> UpsertCore(int userId, UpsertUserCardRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        if (dto.CardPrintingId <= 0)
        {
            return this.CreateValidationProblem("cardPrintingId", "CardPrintingId must be positive.");
        }

        if (await _db.Users.FindAsync(userId) is null)
        {
            if (IsAdmin())
            {
                return this.CreateProblem(StatusCodes.Status404NotFound, detail: "User not found.");
            }

            return Forbid();
        }

        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: "CardPrinting not found.");
        }

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
        if (uc is null) return this.CreateProblem(StatusCodes.Status404NotFound);

        uc.QuantityOwned = Math.Max(0, dto.QuantityOwned);
        uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
        uc.QuantityProxyOwned = Math.Max(0, dto.QuantityProxyOwned);

        // Do NOT remove row when zero.
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetOwnedProxyCore(int userId, int cardPrintingId, SetOwnedProxyRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        if (dto.OwnedQty < 0 || dto.ProxyQty < 0)
        {
            var errors = new Dictionary<string, string[]>();
            if (dto.OwnedQty < 0)
            {
                errors["ownedQty"] = new[] { "Quantity must be non-negative." };
            }

            if (dto.ProxyQty < 0)
            {
                errors["proxyQty"] = new[] { "Quantity must be non-negative." };
            }

            return this.CreateValidationProblem(errors);
        }

        var existing = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);

        if (existing is null)
        {
            var newUserCard = new UserCard
            {
                UserId = userId,
                CardPrintingId = cardPrintingId,
                QuantityOwned = dto.OwnedQty,
                QuantityWanted = 0,
                QuantityProxyOwned = dto.ProxyQty
            };
            _db.UserCards.Add(newUserCard);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        var request = new SetUserCardQuantitiesRequest(dto.OwnedQty, existing.QuantityWanted, dto.ProxyQty);
        return await SetQuantitiesCore(userId, cardPrintingId, request);
    }

    private async Task<IActionResult> PatchQuantitiesCore(int userId, int cardPrintingId, JsonElement updates)
    {
        if (updates.ValueKind != JsonValueKind.Object)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "JSON object required.");
        }

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null) return this.CreateProblem(StatusCodes.Status404NotFound);

        var touched = false;

        if (TryGetInt(updates, "quantityOwned", "QuantityOwned", out var owned))
        {
            uc.QuantityOwned = Math.Max(0, owned);
            touched = true;
        }

        if (TryGetInt(updates, "quantityWanted", "QuantityWanted", out var wanted))
        {
            uc.QuantityWanted = Math.Max(0, wanted);
            touched = true;
        }

        if (TryGetInt(updates, "quantityProxyOwned", "QuantityProxyOwned", out var proxyOwned))
        {
            uc.QuantityProxyOwned = Math.Max(0, proxyOwned);
            touched = true;
        }

        if (!touched) return NoContent();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> ApplyDeltaCore(int userId, IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (deltas is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Deltas payload required.");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            if (IsAdmin())
            {
                return this.CreateProblem(StatusCodes.Status404NotFound, detail: "User not found.");
            }

            return Forbid();
        }

        var deltaList = deltas.ToList();
        if (deltaList.Count == 0) return NoContent();
        if (deltaList.Any(d => d.CardPrintingId <= 0))
        {
            return this.CreateValidationProblem("cardPrintingId", "CardPrintingId must be positive.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        var printingIds = deltaList.Select(d => d.CardPrintingId).Distinct().ToList();
        var validIds = await _db.CardPrintings
            .Where(cp => printingIds.Contains(cp.Id))
            .Select(cp => cp.Id)
            .ToListAsync();
        var validSet = validIds.ToHashSet();

        var missing = printingIds.FirstOrDefault(id => !validSet.Contains(id));
        if (missing != 0)
        {
            return this.CreateValidationProblem(
                "cardPrintingId",
                $"CardPrinting not found: {missing}");
        }

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

            row.QuantityOwned = UserCardMath.AddClamped(row.QuantityOwned, d.DeltaOwned);
            row.QuantityWanted = UserCardMath.AddClamped(row.QuantityWanted, d.DeltaWanted);
            row.QuantityProxyOwned = UserCardMath.AddClamped(row.QuantityProxyOwned, d.DeltaProxyOwned);
        }

        // Do NOT remove rows when zero.
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private async Task<IActionResult> ApplyBulkDeltaCore(int userId, CollectionBulkUpdateRequest request)
    {
        if (request is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "A request body is required." }
                },
                title: "Invalid payload");
        }

        var items = request.Items ?? Array.Empty<CollectionBulkUpdateItem>();
        var deltas = items
            .Select(i => new DeltaUserCardRequest(i.PrintingId, i.OwnedDelta, 0, i.ProxyDelta))
            .ToList();

        var result = await ApplyDeltaCore(userId, deltas);
        // Restore previous behavior: convert NotFound (404) to BadRequest (400)
        if (result is ObjectResult objectResult &&
            objectResult.Value is ProblemDetails problemDetails &&
            problemDetails.Status == StatusCodes.Status404NotFound)
        {
            // Convert to BadRequest (400)
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: problemDetails.Title ?? "Validation error",
                detail: problemDetails.Detail ?? "A validation error occurred.");
        }
        return result;
    }

    private async Task<IActionResult> RemoveCore(int userId, int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null) return this.CreateProblem(StatusCodes.Status404NotFound);
        _db.UserCards.Remove(uc);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------------------
    // Legacy routes (userId in the URL)
    // -----------------------------------------

    [HttpGet]
    public async Task<ActionResult<Paged<CollectionItemDto>>> GetAll(
        int userId,
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (UserMismatch(userId)) return Forbid();
        return await GetAllCore(userId, game, set, rarity, name, cardPrintingId, page, pageSize);
    }

    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertUserCardRequest dto)
    {
        if (UserMismatch(userId)) return Forbid();
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }
        return await UpsertCore(userId, dto);
    }

    [HttpPut("{cardPrintingId:int}")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> SetQuantities(int userId, int cardPrintingId, [FromBody] SetUserCardQuantitiesRequest dto)
    {
        if (UserMismatch(userId)) return Forbid();
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }
        return await SetQuantitiesCore(userId, cardPrintingId, dto);
    }

    [HttpPatch("{cardPrintingId:int}")]
    public async Task<IActionResult> PatchQuantities(int userId, int cardPrintingId, [FromBody] JsonElement patch)
    {
        if (UserMismatch(userId)) return Forbid();
        return await PatchQuantitiesCore(userId, cardPrintingId, patch);
    }

    [HttpPost("delta")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyDelta(int userId, [FromBody] IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (UserMismatch(userId)) return Forbid();
        if (deltas is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Deltas payload required.");
        }
        return await ApplyDeltaCore(userId, deltas);
    }

    [HttpPatch("bulk")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyBulkDelta(int userId, [FromBody] CollectionBulkUpdateRequest request)
    {
        if (UserMismatch(userId)) return Forbid();
        return await ApplyBulkDeltaCore(userId, request);
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

    [HttpPost("/api/collection/items")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<QuickAddResponse>> QuickAddForCurrent([FromBody] QuickAddRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        var errors = new Dictionary<string, string[]>();
        if (dto.PrintingId <= 0)
        {
            errors["printingId"] = new[] { "printingId must be positive." };
        }

        if (dto.Quantity <= 0)
        {
            errors["quantity"] = new[] { "Quantity must be positive." };
        }

        if (errors.Count > 0)
        {
            return this.CreateValidationProblem(errors);
        }

        if (await _db.CardPrintings.FindAsync(dto.PrintingId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: "CardPrinting not found.");
        }

        var card = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == uid && x.CardPrintingId == dto.PrintingId);

        if (card is null)
        {
            card = new UserCard
            {
                UserId = uid,
                CardPrintingId = dto.PrintingId,
                QuantityOwned = dto.Quantity,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            };
            _db.UserCards.Add(card);
        }
        else
        {
            card.QuantityOwned = UserCardMath.AddClamped(card.QuantityOwned, dto.Quantity);
        }

        await _db.SaveChangesAsync();
        return Ok(new QuickAddResponse(dto.PrintingId, card.QuantityOwned));
    }

    [HttpGet("/api/collection")]
    [HttpGet("/api/collections")]
    public async Task<ActionResult<Paged<CollectionItemDto>>> GetAllForCurrent(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await GetAllCore(uid, game, set, rarity, name, cardPrintingId, page, pageSize);
    }

    [HttpPost("/api/collection")]
    [HttpPost("/api/collections")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> UpsertForCurrent([FromBody] UpsertUserCardRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }
        return await UpsertCore(uid, dto);
    }

    [HttpPut("/api/collection/{cardPrintingId:int}")]
    [HttpPut("/api/collections/{cardPrintingId:int}")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> SetOwnedProxyForCurrent(int cardPrintingId, [FromBody] SetOwnedProxyRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await SetOwnedProxyCore(uid, cardPrintingId, dto);
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
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyDeltaForCurrent([FromBody] IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        if (deltas is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Deltas payload required.");
        }
        return await ApplyDeltaCore(uid, deltas);
    }

    [HttpPatch("/api/collection/bulk")]
    [HttpPatch("/api/collections/bulk")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyBulkDeltaForCurrent([FromBody] CollectionBulkUpdateRequest request)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await ApplyBulkDeltaCore(uid, request);
    }

    [HttpDelete("/api/collection/{cardPrintingId:int}")]
    [HttpDelete("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> RemoveForCurrent(int cardPrintingId)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await RemoveCore(uid, cardPrintingId);
    }
}
