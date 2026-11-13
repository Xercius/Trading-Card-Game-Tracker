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
        if (pageSize > 100) pageSize = 100;

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

        if (dto.QuantityOwned.HasValue)
        {
            uc.QuantityOwned = QuantityGuards.Clamp(dto.QuantityOwned.Value);
        }
        if (dto.QuantityWanted.HasValue)
        {
            uc.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted.Value);
        }
        if (dto.QuantityProxyOwned.HasValue)
        {
            uc.QuantityProxyOwned = QuantityGuards.Clamp(dto.QuantityProxyOwned.Value);
        }

        await _db.SaveChangesAsync();
        return NoContent();
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

        if (TryGetInt(updates, "quantityOwned", "QuantityOwned", out var owned) ||
            TryGetInt(updates, "ownedQty", "OwnedQty", out owned))
        {
            uc.QuantityOwned = QuantityGuards.Clamp(owned);
            touched = true;
        }

        if (TryGetInt(updates, "quantityWanted", "QuantityWanted", out var wanted) ||
            TryGetInt(updates, "wantedQty", "WantedQty", out wanted))
        {
            uc.QuantityWanted = QuantityGuards.Clamp(wanted);
            touched = true;
        }

        if (TryGetInt(updates, "quantityProxyOwned", "QuantityProxyOwned", out var proxyOwned) ||
            TryGetInt(updates, "proxyQty", "ProxyQty", out proxyOwned))
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

    [HttpGet("{cardPrintingId:int}")]
    [HttpGet("/api/collections/{cardPrintingId:int}")]
    public async Task<ActionResult<UserCardQuantitiesResponse>> GetByPrintingId(int cardPrintingId)
    {
        if (cardPrintingId <= 0)
        {
            return this.CreateValidationProblem("cardPrintingId", "CardPrintingId must be positive.");
        }

        var uc = await _db.UserCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);

        if (uc is null)
        {
            var exists = await _db.CardPrintings.AnyAsync(cp => cp.Id == cardPrintingId);
            if (!exists)
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"Card printing {cardPrintingId} was not found.");
            }

            return Ok(new UserCardQuantitiesResponse(cardPrintingId, 0, 0, 0));
        }

        return Ok(new UserCardQuantitiesResponse(
            cardPrintingId,
            uc.QuantityOwned,
            uc.QuantityProxyOwned,
            uc.QuantityWanted));
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
    public async Task<IActionResult> SetQuantities(int cardPrintingId, [FromBody] SetUserCardQuantitiesRequest dto)
    {
        return await SetQuantitiesCore(cardPrintingId, dto);
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

    [HttpPost("batch-set")]
    [HttpPost("/api/collections/batch-set")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> BatchSet([FromBody] CollectionBatchSetRequest request)
    {
        if (request?.Items is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]> { ["request"] = new[] { "A request body is required." } },
                title: "Invalid payload");
        }

        if (request.Items.Count == 0) return NoContent();

        using var tx = await _db.Database.BeginTransactionAsync();
        var printingIds = request.Items.Select(i => i.CardPrintingId).Distinct().ToList();
        var validIds = await _db.CardPrintings
            .Where(cp => printingIds.Contains(cp.Id))
            .Select(cp => cp.Id)
            .ToListAsync();
        var validSet = validIds.ToHashSet();

        var map = await _db.UserCards
            .Where(uc => uc.UserId == UserId && printingIds.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        foreach (var item in request.Items)
        {
            if (!validSet.Contains(item.CardPrintingId)) continue;

            var owned = QuantityGuards.Clamp(item.QuantityOwned);
            var proxy = QuantityGuards.Clamp(item.QuantityProxyOwned);
            var wanted = QuantityGuards.Clamp(item.QuantityWanted);

            if (!map.TryGetValue(item.CardPrintingId, out var row))
            {
                if (owned == 0 && proxy == 0 && wanted == 0)
                    continue;

                row = new UserCard
                {
                    UserId = UserId,
                    CardPrintingId = item.CardPrintingId,
                    QuantityOwned = owned,
                    QuantityProxyOwned = proxy,
                    QuantityWanted = wanted
                };
                _db.UserCards.Add(row);
                map[item.CardPrintingId] = row;
            }
            else
            {
                row.QuantityOwned = owned;
                row.QuantityProxyOwned = proxy;
                row.QuantityWanted = wanted;
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpDelete("{cardPrintingId:int}")]
    [HttpDelete("/api/collections/{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(int cardPrintingId)
    {
        return await RemoveCore(cardPrintingId);
    }

    /// <summary>
    /// Computes collection completion progress grouped by Set and Treatment subset for a given game.
    /// </summary>
    /// <param name="userId">Optional user ID (defaults to current default user).</param>
    /// <param name="game">Game name to filter by (e.g. "Star Wars Unlimited").</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("sets-summary")]
    [HttpGet("/api/collections/sets-summary")]
    [HttpGet("/api/user/{userId:int}/collection/sets-summary")]
    [HttpGet("/api/user/{userId:int}/collections/sets-summary")]
    public async Task<ActionResult<CollectionSetSummaryResponse>> GetSetsSummary(
        int? userId,
        [FromQuery] string? game,
        CancellationToken ct = default)
    {
        var targetUserId = userId ?? UserId;

        // 1. Resolve Game IDs if game filter is provided
        var games = CsvUtils.Parse(game);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsNoTracking().AsQueryable();
        if (games.Count > 0)
        {
            gameLookupQuery = gameLookupQuery.Where(g => normalizedGames.Contains(g.NameNormalized));
        }
        var gameIds = await gameLookupQuery.Select(g => g.Id).ToListAsync(ct);

        // 2. Query Sets for these games
        var setsQuery = _db.Sets.AsNoTracking().AsQueryable();
        if (games.Count > 0)
        {
            setsQuery = setsQuery.Where(s => gameIds.Contains(s.GameId));
        }

        var sets = await setsQuery
            .Include(s => s.Game)
            .OrderBy(s => s.ReleasedAt)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        if (sets.Count == 0)
        {
            return Ok(new CollectionSetSummaryResponse([], 0, 0, 0));
        }

        var setIds = sets.Select(s => s.Id).ToList();

        // 3. Query all CardPrintings in these sets
        var printings = await _db.CardPrintings.AsNoTracking()
            .Where(cp => cp.SetId != null && setIds.Contains(cp.SetId.Value))
            .Select(cp => new
            {
                cp.Id,
                SetId = cp.SetId!.Value,
                cp.TreatmentId,
                TreatmentName = cp.Treatment != null ? cp.Treatment.Name : "Standard",
                cp.IsFoil
            })
            .ToListAsync(ct);

        // 4. Query user's cards for these printings
        var allPrintingIds = printings.Select(p => p.Id).ToList();
        var userCards = await _db.UserCards.AsNoTracking()
            .Where(uc => uc.UserId == targetUserId && allPrintingIds.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId, uc => new { uc.QuantityOwned, uc.QuantityProxyOwned }, ct);

        // 5. Build set summaries
        var setSummaries = new List<CollectionSetSummaryDto>();
        var allPrintingIdsInGame = new HashSet<int>();
        var allOwnedPrintingIdsInGame = new HashSet<int>();

        foreach (var set in sets)
        {
            bool isVirtualSet = IsVirtualSet(set.Game?.Name, set.Name);
            var setPrintings = printings.Where(p => p.SetId == set.Id).ToList();
            if (setPrintings.Count == 0)
            {
                setSummaries.Add(new CollectionSetSummaryDto(
                    set.Id,
                    set.Code,
                    set.Name ?? set.Code,
                    set.ReleasedAt?.Year,
                    set.ReleasedAt,
                    isVirtualSet,
                    0,
                    0,
                    0,
                    []
                ));
                continue;
            }

            var printingIdsInSet = setPrintings.Select(p => p.Id).ToHashSet();
            allPrintingIdsInGame.UnionWith(printingIdsInSet);

            var ownedPrintingIdsInSet = new HashSet<int>();
            foreach (var p in setPrintings)
            {
                if (userCards.TryGetValue(p.Id, out var uc))
                {
                    bool isOwned = isVirtualSet
                        ? (uc.QuantityOwned > 0 || uc.QuantityProxyOwned > 0)
                        : (uc.QuantityOwned > 0);

                    if (isOwned)
                    {
                        ownedPrintingIdsInSet.Add(p.Id);
                        allOwnedPrintingIdsInGame.Add(p.Id);
                    }
                }
            }

            int totalSetCards = printingIdsInSet.Count;
            int ownedSetCards = ownedPrintingIdsInSet.Count;
            double setPct = totalSetCards > 0 ? Math.Round((double)ownedSetCards / totalSetCards * 100, 1) : 0;

            // Group subsets by treatment
            var subsets = new List<CollectionSubsetSummaryDto>();
            var treatmentGroups = setPrintings
                .GroupBy(p => new { p.TreatmentId, TreatmentName = p.TreatmentName ?? "Standard" })
                .OrderBy(g => g.Key.TreatmentName == "Standard" ? 0 : 1)
                .ThenBy(g => g.Key.TreatmentName);

            foreach (var tg in treatmentGroups)
            {
                int totalTreatmentCards = tg.Count();
                int ownedTreatmentCards = 0;

                foreach (var p in tg)
                {
                    if (userCards.TryGetValue(p.Id, out var uc))
                    {
                        bool isOwned = isVirtualSet
                            ? (uc.QuantityOwned > 0 || uc.QuantityProxyOwned > 0)
                            : (uc.QuantityOwned > 0);

                        if (isOwned) ownedTreatmentCards++;
                    }
                }

                double subsetPct = totalTreatmentCards > 0
                    ? Math.Round((double)ownedTreatmentCards / totalTreatmentCards * 100, 1)
                    : 0;

                subsets.Add(new CollectionSubsetSummaryDto(
                    tg.Key.TreatmentId,
                    tg.Key.TreatmentName,
                    totalTreatmentCards,
                    ownedTreatmentCards,
                    subsetPct
                ));
            }

            setSummaries.Add(new CollectionSetSummaryDto(
                set.Id,
                set.Code,
                set.Name ?? set.Code,
                set.ReleasedAt?.Year,
                set.ReleasedAt,
                isVirtualSet,
                totalSetCards,
                ownedSetCards,
                setPct,
                subsets
            ));
        }

        int totalInGame = allPrintingIdsInGame.Count;
        int totalOwnedInGame = allOwnedPrintingIdsInGame.Count;
        double overallPct = totalInGame > 0 ? Math.Round((double)totalOwnedInGame / totalInGame * 100, 1) : 0;

        return Ok(new CollectionSetSummaryResponse(
            setSummaries,
            totalOwnedInGame,
            totalInGame,
            overallPct
        ));
    }

    private static bool IsVirtualSet(string? gameName, string? setName) =>
        !string.IsNullOrWhiteSpace(gameName)
        && (gameName.Contains("Customizable", StringComparison.OrdinalIgnoreCase) || gameName.Equals("SWCCG", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(setName)
        && (setName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || setName.Equals("Demo Deck", StringComparison.OrdinalIgnoreCase));
}
