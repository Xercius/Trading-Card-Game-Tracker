using System.Text.Json;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using api.Data;
using api.Features.Decks.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Decks;

[ApiController]
[RequireUserHeader]
// Legacy, user-scoped routes for compatibility:
[Route("api/user/{userId:int}/deck")]
public class DecksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public DecksController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private bool TryResolveCurrentUserId(out int userId, out IActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null) { error = StatusCode(403, "User missing."); userId = 0; return false; }
        error = null; userId = me.Id; return true;
    }

    private async Task<(JsonElement payload, IActionResult? error)> ReadJsonBodyAsync(string errorMessage)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body);
            if (doc.RootElement.ValueKind == JsonValueKind.Undefined)
                return (default, BadRequest(errorMessage));
            return (doc.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return (default, BadRequest("Invalid JSON payload."));
        }
    }

    private bool UserMismatch(int userId)
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || (!me.IsAdmin && me.Id != userId);
    }

    private bool NotOwnerAndNotAdmin(Deck d)
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || (!me.IsAdmin && me.Id != d.UserId);
    }

    private async Task<(Deck? deck, IActionResult? error)> GetDeckForCaller(int deckId)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return (null, NotFound());
        if (NotOwnerAndNotAdmin(d)) return (null, StatusCode(403, "Forbidden"));
        return (d, null);
    }

    private static int NN(long v) => v < 0 ? 0 : v > int.MaxValue ? int.MaxValue : (int)v;

    private static bool TryI32(JsonElement e, string a, string b, out int v)
    {
        v = 0;
        return (e.TryGetProperty(a, out var p) && p.TryGetInt32(out v))
            || (e.TryGetProperty(b, out p) && p.TryGetInt32(out v));
    }

    // -----------------------------
    // Core: Decks (single source of truth)
    // -----------------------------

    private async Task<IActionResult> ListUserDecksCore(int userId, string? game, string? name, bool? hasCards)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        var query = _db.Decks.Where(d => d.UserId == userId).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(game))
        {
            var g = game.Trim();
            query = query.Where(d => d.Game == g);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pat = $"%{name.Trim()}%";
            query = query.Where(d => EF.Functions.Like(d.Name, pat));
        }

        var decks = await query.ToListAsync();

        if (hasCards == true && decks.Count > 0)
        {
            var ids = decks.Select(d => d.Id).ToList();
            var withCards = await _db.DeckCards
                .Where(dc => ids.Contains(dc.DeckId))
                .GroupBy(dc => dc.DeckId)
                .Select(g => g.Key)
                .ToListAsync();
            var set = withCards.ToHashSet();
            decks = decks.Where(d => set.Contains(d.Id)).ToList();
        }

        var responses = _mapper.Map<List<DeckResponse>>(decks);
        return Ok(responses);
    }

    private async Task<IActionResult> CreateDeckCore(int userId, CreateDeckRequest dto)
    {
        if (dto is null) return BadRequest();
        if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Game and Name required.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        var name = dto.Name.Trim();
        var game = dto.Game.Trim();

        if (await _db.Decks.AnyAsync(x => x.UserId == userId && x.Name.ToLower() == name.ToLower()))
            return Conflict("Duplicate deck name for user.");

        var deck = _mapper.Map<Deck>(dto);
        deck.UserId = userId;
        deck.Game = game;
        deck.Name = name;

        _db.Decks.Add(deck);
        await _db.SaveChangesAsync();

        var response = _mapper.Map<DeckResponse>(deck);
        return CreatedAtAction(nameof(GetDeck), new { deckId = deck.Id }, response);
    }

    private async Task<IActionResult> GetDeckCore(int deckId)
    {
        var d = await _db.Decks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
        return Ok(_mapper.Map<DeckResponse>(d));
    }

    private async Task<IActionResult> PatchDeckCore(int deckId, JsonElement updates)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");

        if (updates.TryGetProperty("game", out var g) && g.ValueKind == JsonValueKind.String)
            d.Game = g.GetString()!.Trim();

        if (updates.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            var targetName = n.GetString()!.Trim();
            if (await _db.Decks.AnyAsync(x => x.UserId == d.UserId && x.Id != d.Id && x.Name.ToLower() == targetName.ToLower()))
                return Conflict("Duplicate deck name for user.");
            d.Name = targetName;
        }
        if (updates.TryGetProperty("description", out var desc))
            d.Description = desc.ValueKind == JsonValueKind.Null ? null :
                            desc.ValueKind == JsonValueKind.String ? desc.GetString() : d.Description;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> UpdateDeckCore(int deckId, UpdateDeckRequest dto)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
        if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Game and Name required.");

        var targetName = dto.Name.Trim();
        if (await _db.Decks.AnyAsync(x => x.UserId == d.UserId && x.Id != d.Id && x.Name.ToLower() == targetName.ToLower()))
            return Conflict("Duplicate deck name for user.");

        _mapper.Map(dto, d);
        d.Name = targetName;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> DeleteDeckCore(int deckId)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return StatusCode(403, "User mismatch.");
        _db.Decks.Remove(d);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------
    // Core: Deck Cards (ownership enforced)
    // -----------------------------

    private async Task<IActionResult> GetDeckCardsCore(int deckId)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;

        var rows = await _db.DeckCards
            .Where(dc => dc.DeckId == deckId)
            .Include(dc => dc.CardPrinting).ThenInclude(cp => cp.Card)
            .AsNoTracking()
            .ProjectTo<DeckCardItemResponse>(_mapper.ConfigurationProvider)
            .ToListAsync();

        return Ok(rows);
    }

    private async Task<IActionResult> UpsertDeckCardCore(int deckId, UpsertDeckCardRequest dto)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;
        if (dto is null) return BadRequest();

        var cp = await _db.CardPrintings.Include(x => x.Card).FirstOrDefaultAsync(x => x.Id == dto.CardPrintingId);
        if (cp is null) return NotFound("CardPrinting not found.");
        if (!string.Equals(deck!.Game, cp.Card.Game, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Card game does not match deck game.");

        var dc = await _db.DeckCards
            .FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == dto.CardPrintingId);

        if (dc is null)
        {
            dc = new DeckCard
            {
                DeckId = deckId,
                CardPrintingId = dto.CardPrintingId,
                QuantityInDeck = NN(dto.QuantityInDeck),
                QuantityIdea = NN(dto.QuantityIdea),
                QuantityAcquire = NN(dto.QuantityAcquire),
                QuantityProxy = NN(dto.QuantityProxy)
            };
            _db.DeckCards.Add(dc);
        }
        else
        {
            dc.QuantityInDeck = NN(dto.QuantityInDeck);
            dc.QuantityIdea = NN(dto.QuantityIdea);
            dc.QuantityAcquire = NN(dto.QuantityAcquire);
            dc.QuantityProxy = NN(dto.QuantityProxy);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> ApplyDeckCardDeltaCore(int deckId, IEnumerable<DeckCardDeltaRequest> deltas)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;
        if (deltas is null) return BadRequest("Deltas payload required.");

        var list = deltas.ToList();
        if (list.Count == 0) return NoContent();

        var ids = list.Select(x => x.CardPrintingId).Distinct().ToList();
        var printings = await _db.CardPrintings
            .Where(cp => ids.Contains(cp.Id))
            .Include(cp => cp.Card)
            .ToListAsync();

        if (printings.Count != ids.Count) return NotFound("One or more CardPrintings not found.");
        if (printings.Any(cp => !string.Equals(deck!.Game, cp.Card.Game, StringComparison.OrdinalIgnoreCase)))
            return BadRequest("One or more card games do not match deck game.");

        using var tx = await _db.Database.BeginTransactionAsync();

        var map = await _db.DeckCards
            .Where(dc => dc.DeckId == deckId && ids.Contains(dc.CardPrintingId))
            .ToDictionaryAsync(dc => dc.CardPrintingId);

        foreach (var delta in list)
        {
            if (!map.TryGetValue(delta.CardPrintingId, out var row))
            {
                row = new DeckCard
                {
                    DeckId = deckId,
                    CardPrintingId = delta.CardPrintingId,
                    QuantityInDeck = 0,
                    QuantityIdea = 0,
                    QuantityAcquire = 0,
                    QuantityProxy = 0
                };
                _db.DeckCards.Add(row);
                map[delta.CardPrintingId] = row;
            }

            row.QuantityInDeck = NN((long)row.QuantityInDeck + delta.DeltaInDeck);
            row.QuantityIdea = NN((long)row.QuantityIdea + delta.DeltaIdea);
            row.QuantityAcquire = NN((long)row.QuantityAcquire + delta.DeltaAcquire);
            row.QuantityProxy = NN((long)row.QuantityProxy + delta.DeltaProxy);
        }

        var remove = map.Values
            .Where(dc => dc.QuantityInDeck == 0 && dc.QuantityIdea == 0 && dc.QuantityAcquire == 0 && dc.QuantityProxy == 0)
            .ToList();
        if (remove.Count > 0)
        {
            _db.DeckCards.RemoveRange(remove);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetDeckCardQuantitiesCore(int deckId, int cardPrintingId, SetDeckCardQuantitiesRequest dto)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;

        var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
        if (dc is null) return NotFound();

        dc.QuantityInDeck = NN(dto.QuantityInDeck);
        dc.QuantityIdea = NN(dto.QuantityIdea);
        dc.QuantityAcquire = NN(dto.QuantityAcquire);
        dc.QuantityProxy = NN(dto.QuantityProxy);

        if (dc.QuantityInDeck == 0 && dc.QuantityIdea == 0 && dc.QuantityAcquire == 0 && dc.QuantityProxy == 0)
            _db.DeckCards.Remove(dc);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> PatchDeckCardQuantitiesCore(int deckId, int cardPrintingId, JsonElement updates)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;

        var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
        if (dc is null) return NotFound();

        if (TryI32(updates, "quantityInDeck", "QuantityInDeck", out var v1)) dc.QuantityInDeck = NN(v1);
        if (TryI32(updates, "quantityIdea", "QuantityIdea", out var v2)) dc.QuantityIdea = NN(v2);
        if (TryI32(updates, "quantityAcquire", "QuantityAcquire", out var v3)) dc.QuantityAcquire = NN(v3);
        if (TryI32(updates, "quantityProxy", "QuantityProxy", out var v4)) dc.QuantityProxy = NN(v4);

        if (dc.QuantityInDeck == 0 && dc.QuantityIdea == 0 && dc.QuantityAcquire == 0 && dc.QuantityProxy == 0)
            _db.DeckCards.Remove(dc);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> RemoveDeckCardCore(int deckId, int cardPrintingId)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;

        var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
        if (dc is null) return NotFound();

        _db.DeckCards.Remove(dc);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> GetDeckAvailabilityCore(int deckId, bool includeProxies)
    {
        var (deck, err) = await GetDeckForCaller(deckId);
        if (err != null) return err;

        var deckEntity = deck!;

        var deckCards = await _db.DeckCards.Where(dc => dc.DeckId == deckId).ToListAsync();
        if (!deckCards.Any()) return Ok(Array.Empty<DeckAvailabilityItemResponse>());

        var printIds = deckCards.Select(dc => dc.CardPrintingId).ToList();
        var userCards = await _db.UserCards
            .Where(uc => uc.UserId == deckEntity.UserId && printIds.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        var result = new List<DeckAvailabilityItemResponse>();
        foreach (var dc in deckCards)
        {
            userCards.TryGetValue(dc.CardPrintingId, out var uc);
            var owned = uc?.QuantityOwned ?? 0;
            var proxy = uc?.QuantityProxyOwned ?? 0;
            var assigned = dc.QuantityInDeck;
            var available = Math.Max(0, owned - assigned);
            var availableWithProxy = includeProxies ? Math.Max(0, owned + proxy - assigned) : available;

            result.Add(new DeckAvailabilityItemResponse(
                dc.CardPrintingId, owned, proxy, assigned, available, availableWithProxy
            ));
        }

        return Ok(result);
    }

    // -----------------------------------------
    // Legacy (userId in URL)
    // -----------------------------------------

    // GET /api/user/{userId}/deck
    [HttpGet]
    public async Task<IActionResult> GetUserDecks(int userId, [FromQuery] string? game = null, [FromQuery] string? name = null, [FromQuery] bool? hasCards = null)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await ListUserDecksCore(userId, game, name, hasCards);
    }

    // POST /api/user/{userId}/deck
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateDeck(int userId, [FromBody] CreateDeckRequest dto)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await CreateDeckCore(userId, dto);
    }

    // -----------------------------------------
    // Auth-derived aliases (preferred)
    // -----------------------------------------

    // GET /api/deck  (list current user's decks)
    [HttpGet("/api/deck")]
    [HttpGet("/api/decks")] // alias, optional
    public async Task<IActionResult> GetMyDecks([FromQuery] string? game = null, [FromQuery] string? name = null, [FromQuery] bool? hasCards = null)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await ListUserDecksCore(uid, game, name, hasCards);
    }

    // POST /api/deck  (create for current user)
    [HttpPost("/api/deck")]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateMyDeck([FromBody] CreateDeckRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await CreateDeckCore(uid, dto);
    }

    // ----- Deck metadata (ownership enforced) -----

    // GET /api/deck/{deckId}
    [HttpGet("/api/deck/{deckId:int}")]
    [HttpGet("/api/decks/{deckId:int}")] // alias
    public async Task<IActionResult> GetDeck(int deckId) => await GetDeckCore(deckId);

    // PATCH /api/deck/{deckId}
    // PATCH /api/deck/{deckId}
    [HttpPatch("/api/deck/{deckId:int}")]
    [HttpPatch("/api/decks/{deckId:int}")]
    [Consumes("application/json", "application/*+json")]
    public async Task<IActionResult> PatchDeck(int deckId)
    {
        var (updates, error) = await ReadJsonBodyAsync("JSON object required.");
        if (error != null) return error;
        if (updates.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");
        return await PatchDeckCore(deckId, updates);
    }

    // PUT /api/deck/{deckId}
    [HttpPut("/api/deck/{deckId:int}")]
    [HttpPut("/api/decks/{deckId:int}")] // alias
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateDeck(int deckId, [FromBody] UpdateDeckRequest dto) => await UpdateDeckCore(deckId, dto);

    // DELETE /api/deck/{deckId}
    [HttpDelete("/api/deck/{deckId:int}")]
    [HttpDelete("/api/decks/{deckId:int}")] // alias
    public async Task<IActionResult> DeleteDeck(int deckId) => await DeleteDeckCore(deckId);

    // ----- Deck cards (ownership enforced) -----

    // GET /api/deck/{deckId}/cards
    [HttpGet("/api/deck/{deckId:int}/cards")]
    [HttpGet("/api/decks/{deckId:int}/cards")] // alias
    public async Task<IActionResult> GetDeckCards(int deckId) => await GetDeckCardsCore(deckId);

    // POST /api/deck/{deckId}/cards  (upsert one)
    [HttpPost("/api/deck/{deckId:int}/cards")]
    [HttpPost("/api/decks/{deckId:int}/cards")] // alias
    [Consumes("application/json")]
    public async Task<IActionResult> UpsertDeckCard(int deckId, [FromBody] UpsertDeckCardRequest dto)
        => await UpsertDeckCardCore(deckId, dto);

    // POST /api/deck/{deckId}/cards/delta
    [HttpPost("/api/deck/{deckId:int}/cards/delta")]
    [HttpPost("/api/decks/{deckId:int}/cards/delta")] // alias
    [Consumes("application/json")]
    public async Task<IActionResult> ApplyDeckCardDelta(int deckId, [FromBody] IEnumerable<DeckCardDeltaRequest> deltas)
        => await ApplyDeckCardDeltaCore(deckId, deltas);

    // PUT /api/deck/{deckId}/cards/{cardPrintingId}
    [HttpPut("/api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
    [HttpPut("/api/decks/{deckId:int}/cards/{cardPrintingId:int}")] // alias
    [Consumes("application/json")]
    public async Task<IActionResult> SetDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] SetDeckCardQuantitiesRequest dto)
        => await SetDeckCardQuantitiesCore(deckId, cardPrintingId, dto);

    // PATCH /api/deck/{deckId}/cards/{cardPrintingId}
    [HttpPatch("/api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
    [HttpPatch("/api/decks/{deckId:int}/cards/{cardPrintingId:int}")]
    [Consumes("application/json", "application/*+json")]
    public async Task<IActionResult> PatchDeckCardQuantities(int deckId, int cardPrintingId)
    {
        var (updates, error) = await ReadJsonBodyAsync("JSON object required.");
        if (error != null) return error;
        if (updates.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");
        return await PatchDeckCardQuantitiesCore(deckId, cardPrintingId, updates);
    }

    // DELETE /api/deck/{deckId}/cards/{cardPrintingId}
    [HttpDelete("/api/deck/{deckId:int}/cards/{cardPrintingId:int}")]
    [HttpDelete("/api/decks/{deckId:int}/cards/{cardPrintingId:int}")] // alias
    public async Task<IActionResult> RemoveDeckCard(int deckId, int cardPrintingId)
        => await RemoveDeckCardCore(deckId, cardPrintingId);

    // ----- Availability -----

    // GET /api/deck/{deckId}/availability?includeProxies=
    [HttpGet("/api/deck/{deckId:int}/availability")]
    [HttpGet("/api/decks/{deckId:int}/availability")] // alias
    public async Task<IActionResult> GetDeckAvailability(int deckId, [FromQuery] bool includeProxies = false)
        => await GetDeckAvailabilityCore(deckId, includeProxies);
}
