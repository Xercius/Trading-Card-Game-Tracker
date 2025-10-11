using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Collections.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using System.Text.Json;
using CollectionItemDto = api.Features.Collections.Dtos.UserCardItemResponse;

namespace api.Features.Collections;

[ApiController]
[Authorize]
[Route("api/user/{userId:int}/collection")]
// TODO: Eventually remove legacy userId routes after clients migrate to /api/collection.
public class CollectionsController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;

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
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"User {userId} was not found.");
            }

            return Forbid();
        }

        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0) pageSize = DefaultPageSize;

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.UserId == userId)
            .AsNoTracking()
            .FilterByPrintingMetadata(game, set, rarity, name, cardPrintingId, useCaseInsensitiveName: false);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByCardNameAndPrinting()
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
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"User {userId} was not found.");
            }

            return Forbid();
        }

        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.CardPrintingId} was not found.");
        }

        var existing = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (existing is null)
        {
            var newCard = new UserCard
            {
                UserId = userId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned),
                QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted),
                QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned)
            };

            // Optional: skip creating an all-zero row on first insert.
            if (IsZero(newCard)) return NoContent();

            _db.UserCards.Add(newCard);
        }
        else
        {
            existing.QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned);
            existing.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted);
            existing.QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned);

            // IMPORTANT: do NOT delete when all quantities are zero.
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetQuantitiesCore(int userId, int cardPrintingId, SetUserCardQuantitiesRequest dto)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User card for user {userId} and card printing {cardPrintingId} was not found.");
        }

        uc.QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned);
        uc.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted);
        uc.QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned);

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
                QuantityOwned = QuantityGuards.Clamp(dto.OwnedQty),
                QuantityWanted = 0,
                QuantityProxyOwned = QuantityGuards.Clamp(dto.ProxyQty)
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
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User card for user {userId} and card printing {cardPrintingId} was not found.");
        }

        var touched = false;

        if (TryGetInt(updates, "quantityOwned", "QuantityOwned", out var owned))
        {
            uc.QuantityOwned = QuantityGuards.Clamp(owned);
            touched = true;
        }

        if (TryGetInt(updates, "quantityWanted", "QuantityWanted", out var wanted))
        {
            uc.QuantityWanted = QuantityGuards.Clamp(wanted);
            touched = true;
        }

        if (TryGetInt(updates, "quantityProxyOwned", "QuantityProxyOwned", out var proxyOwned))
        {
            uc.QuantityProxyOwned = QuantityGuards.Clamp(proxyOwned);
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
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "Deltas payload required." }
                },
                title: "Invalid payload");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            if (IsAdmin())
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"User {userId} was not found.");
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
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {missing} was not found.");
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

            row.QuantityOwned = QuantityGuards.ClampDelta(row.QuantityOwned, d.DeltaOwned);
            row.QuantityWanted = QuantityGuards.ClampDelta(row.QuantityWanted, d.DeltaWanted);
            row.QuantityProxyOwned = QuantityGuards.ClampDelta(row.QuantityProxyOwned, d.DeltaProxyOwned);
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

        return await ApplyDeltaCore(userId, deltas);
    }

    private async Task<IActionResult> RemoveCore(int userId, int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User card for user {userId} and card printing {cardPrintingId} was not found.");
        }
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
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.PrintingId} was not found.");
        }

        var card = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == uid && x.CardPrintingId == dto.PrintingId);

        if (card is null)
        {
            card = new UserCard
            {
                UserId = uid,
                CardPrintingId = dto.PrintingId,
                QuantityOwned = QuantityGuards.Clamp(dto.Quantity),
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            };
            _db.UserCards.Add(card);
        }
        else
        {
            card.QuantityOwned = QuantityGuards.ClampDelta(card.QuantityOwned, dto.Quantity);
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
