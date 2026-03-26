using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Collections.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using System.Text.Json;
using CollectionItemDto = api.Features.Collections.Dtos.UserCardItemResponse;

namespace api.Features.Collections;

[ApiController]
[Route("api/collection")]
public class CollectionsController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;
    private const int UserId = DbSeeder.DefaultUserId;

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

    private static bool TryGetInt(JsonElement obj, string camelName, string pascalName, out int value)
    {
        value = 0;
        return (obj.TryGetProperty(camelName, out var camel) && camel.TryGetInt32(out value))
            || (obj.TryGetProperty(pascalName, out var pascal) && pascal.TryGetInt32(out value));
    }

    private static bool IsZero(UserCard card) =>
        card.QuantityOwned == 0 && card.QuantityWanted == 0 && card.QuantityProxyOwned == 0;

    // -----------------------------
    // Core (single source of logic)
    // -----------------------------

    private async Task<ActionResult<Paged<CollectionItemDto>>> GetAllCore(
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        int page,
        int pageSize)
    {
        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0) pageSize = DefaultPageSize;

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.UserId == UserId)
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

    private async Task<IActionResult> UpsertCore(UpsertUserCardRequest dto)
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

        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.CardPrintingId} was not found.");
        }

        var existing = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == dto.CardPrintingId);

        if (existing is null)
        {
            var newCard = new UserCard
            {
                UserId = UserId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned),
                QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted),
                QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned)
            };

            if (IsZero(newCard)) return NoContent();

            _db.UserCards.Add(newCard);
        }
        else
        {
            existing.QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned);
            existing.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted);
            existing.QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetQuantitiesCore(int cardPrintingId, SetUserCardQuantitiesRequest dto)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {cardPrintingId} was not found in your collection.");
        }

        uc.QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned);
        uc.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted);
        uc.QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetOwnedProxyCore(int cardPrintingId, SetOwnedProxyRequest dto)
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
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);

        if (existing is null)
        {
            var newUserCard = new UserCard
            {
                UserId = UserId,
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
        return await SetQuantitiesCore(cardPrintingId, request);
    }

    private async Task<IActionResult> PatchQuantitiesCore(int cardPrintingId, JsonElement updates)
    {
        if (updates.ValueKind != JsonValueKind.Object)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "JSON object required.");
        }

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {cardPrintingId} was not found in your collection.");
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

    private async Task<IActionResult> ApplyDeltaCore(IEnumerable<DeltaUserCardRequest> deltas)
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
            .Where(uc => uc.UserId == UserId && printingIds.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        foreach (var d in deltaList)
        {
            if (!map.TryGetValue(d.CardPrintingId, out var row))
            {
                row = new UserCard
                {
                    UserId = UserId,
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

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private async Task<IActionResult> ApplyBulkDeltaCore(CollectionBulkUpdateRequest request)
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

        return await ApplyDeltaCore(deltas);
    }

    private async Task<IActionResult> RemoveCore(int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {cardPrintingId} was not found in your collection.");
        }
        _db.UserCards.Remove(uc);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------------------
    // Routes
    // -----------------------------------------

    [HttpGet]
    [HttpGet("/api/collections")]
    public async Task<ActionResult<Paged<CollectionItemDto>>> GetAll(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetAllCore(game, set, rarity, name, cardPrintingId, page, pageSize);
    }

    [HttpPost("items")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<QuickAddResponse>> QuickAdd([FromBody] QuickAddRequest dto)
    {
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
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == dto.PrintingId);

        if (card is null)
        {
            card = new UserCard
            {
                UserId = UserId,
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

    [HttpPost]
    [HttpPost("/api/collections")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> Upsert([FromBody] UpsertUserCardRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }
        return await UpsertCore(dto);
    }

    [HttpPut("{cardPrintingId:int}")]
    [HttpPut("/api/collections/{cardPrintingId:int}")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> SetOwnedProxy(int cardPrintingId, [FromBody] SetOwnedProxyRequest dto)
    {
        return await SetOwnedProxyCore(cardPrintingId, dto);
    }

    [HttpPatch("{cardPrintingId:int}")]
    [HttpPatch("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> PatchQuantities(int cardPrintingId, [FromBody] JsonElement patch)
    {
        return await PatchQuantitiesCore(cardPrintingId, patch);
    }

    [HttpPost("delta")]
    [HttpPost("/api/collections/delta")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyDelta([FromBody] IEnumerable<DeltaUserCardRequest> deltas)
    {
        if (deltas is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Deltas payload required.");
        }
        return await ApplyDeltaCore(deltas);
    }

    [HttpPatch("bulk")]
    [HttpPatch("/api/collections/bulk")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ApplyBulkDelta([FromBody] CollectionBulkUpdateRequest request)
    {
        return await ApplyBulkDeltaCore(request);
    }

    [HttpDelete("{cardPrintingId:int}")]
    [HttpDelete("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(int cardPrintingId)
    {
        return await RemoveCore(cardPrintingId);
    }
}
