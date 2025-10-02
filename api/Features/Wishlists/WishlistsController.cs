using System.Text.Json;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using api.Data;
using api.Features.Wishlists.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WishlistItemDto = api.Features.Wishlists.Dtos.WishlistItemResponse;

namespace api.Features.Wishlists;

[ApiController]
[RequireUserHeader]
[Route("api/user/{userId:int}/wishlist")]
// NOTE: Keep legacy {userId}-based routes for now; prefer /api/wishlist aliases below.
public class WishlistsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public WishlistsController(AppDbContext db, IMapper mapper)
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

    private bool TryResolveCurrentUserId(out int userId, out IActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null) { error = Forbid(); userId = 0; return false; }
        error = null; userId = me.Id; return true;
    }

    // -----------------------------
    // Core (single source of logic)
    // -----------------------------

    // GET list (filters applied in DB)
    private async Task<ActionResult<Paged<WishlistItemDto>>> GetAllCore(
        int userId,
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        int page,
        int pageSize)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 50;

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.UserId == userId && uc.QuantityWanted > 0)
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
            var loweredName = name.ToLower();
            query = query.Where(uc => uc.CardPrinting.Card.Name.ToLower().Contains(loweredName));
        }
        if (cardPrintingId.HasValue)
            query = query.Where(uc => uc.CardPrintingId == cardPrintingId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<WishlistItemDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(new Paged<WishlistItemDto>(items, total, page, pageSize));
    }

    // POST upsert one (wanted)
    private async Task<IActionResult> UpsertCore(int userId, UpsertWishlistRequest dto)
    {
        if (dto is null) return BadRequest();
        if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
        if (await _db.Users.FindAsync(userId) is null) return NotFound("User not found.");
        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null) return NotFound("CardPrinting not found.");

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            uc = new UserCard
            {
                UserId = userId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = 0,
                QuantityProxyOwned = 0,
                QuantityWanted = Math.Max(0, dto.QuantityWanted)
            };
            _db.UserCards.Add(uc);
        }
        else
        {
            uc.QuantityWanted = Math.Max(0, dto.QuantityWanted);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT bulk set (wanted)
    private async Task<IActionResult> BulkSetCore(int userId, IEnumerable<BulkSetWishlistRequest> items)
    {
        if (items is null) return BadRequest("Payload required.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        var list = items.ToList();
        if (list.Count == 0) return NoContent();
        if (list.Any(i => i.CardPrintingId <= 0)) return BadRequest("CardPrintingId must be positive.");

        var ids = list.Select(i => i.CardPrintingId).Distinct().ToList();
        var validIds = await _db.CardPrintings.Where(cp => ids.Contains(cp.Id)).Select(cp => cp.Id).ToListAsync();
        var missing = ids.FirstOrDefault(id => !validIds.Contains(id));
        if (missing != 0) return NotFound($"CardPrinting not found: {missing}");

        var map = await _db.UserCards
            .Where(uc => uc.UserId == userId && ids.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        foreach (var i in list)
        {
            if (!map.TryGetValue(i.CardPrintingId, out var uc))
            {
                uc = new UserCard
                {
                    UserId = userId,
                    CardPrintingId = i.CardPrintingId,
                    QuantityOwned = 0,
                    QuantityProxyOwned = 0,
                    QuantityWanted = 0
                };
                _db.UserCards.Add(uc);
                map[i.CardPrintingId] = uc;
            }

            uc.QuantityWanted = Math.Max(0, i.QuantityWanted);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST move-to-collection (decrement wanted, increment owned/proxy)
    private async Task<IActionResult> MoveToCollectionCore(int userId, MoveToCollectionRequest dto)
    {
        if (dto is null) return BadRequest();
        if (dto.CardPrintingId <= 0) return BadRequest("CardPrintingId required.");
        if (dto.Quantity <= 0) return BadRequest("Quantity must be positive.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            // Create the row if it doesn't exist; wanted will floor at 0 below.
            uc = new UserCard
            {
                UserId = userId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = 0,
                QuantityProxyOwned = 0,
                QuantityWanted = 0
            };
            _db.UserCards.Add(uc);
        }

        // Decrease wishlist, floor at 0
        uc.QuantityWanted = Math.Max(0, uc.QuantityWanted - dto.Quantity);

        // Increase either owned or proxy
        if (dto.UseProxy)
            uc.QuantityProxyOwned = Math.Max(0, uc.QuantityProxyOwned + dto.Quantity);
        else
            uc.QuantityOwned = Math.Max(0, uc.QuantityOwned + dto.Quantity);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE (set wanted to 0; remove row if all counts are 0 after)
    private async Task<IActionResult> RemoveCore(int userId, int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null) return NotFound();

        uc.QuantityWanted = 0;

        // Optional clean-up: if fully zero, remove the row
        if (uc.QuantityOwned == 0 && uc.QuantityProxyOwned == 0 && uc.QuantityWanted == 0)
            _db.UserCards.Remove(uc);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------------------
    // Legacy routes (userId in the URL)
    // -----------------------------------------

    [HttpGet]
    public async Task<ActionResult<Paged<WishlistItemDto>>> GetAll(
        int userId,
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await GetAllCore(userId, game, set, rarity, name, cardPrintingId, page, pageSize);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertWishlistRequest dto)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await UpsertCore(userId, dto);
    }

    [HttpPut]
    public async Task<IActionResult> BulkSet(int userId, [FromBody] IEnumerable<BulkSetWishlistRequest> items)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await BulkSetCore(userId, items);
    }

    [HttpPost("move-to-collection")]
    public async Task<IActionResult> MoveToCollection(int userId, [FromBody] MoveToCollectionRequest dto)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await MoveToCollectionCore(userId, dto);
    }

    [HttpDelete("{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(int userId, int cardPrintingId)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await RemoveCore(userId, cardPrintingId);
    }

    // -----------------------------------------
    // Auth-derived alias routes (/api/wishlist)
    // -----------------------------------------

    [HttpGet("/api/wishlist")]
    public async Task<ActionResult<Paged<WishlistItemDto>>> GetAllForCurrent(
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

    [HttpPost("/api/wishlist")]
    public async Task<IActionResult> UpsertForCurrent([FromBody] UpsertWishlistRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await UpsertCore(uid, dto);
    }

    [HttpPut("/api/wishlist")]
    public async Task<IActionResult> BulkSetForCurrent([FromBody] IEnumerable<BulkSetWishlistRequest> items)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await BulkSetCore(uid, items);
    }

    [HttpPost("/api/wishlist/move-to-collection")]
    public async Task<IActionResult> MoveToCollectionForCurrent([FromBody] MoveToCollectionRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await MoveToCollectionCore(uid, dto);
    }

    [HttpDelete("/api/wishlist/{cardPrintingId:int}")]
    public async Task<IActionResult> RemoveForCurrent(int cardPrintingId)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await RemoveCore(uid, cardPrintingId);
    }
}
