using api.Data;
using api.Features.Decks.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeckDto = api.Features.Decks.Dtos.DeckResponse;

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

    private bool TryResolveCurrentUserId(out int userId, out ActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null) { error = Forbid(); userId = 0; return false; }
        error = null; userId = me.Id; return true;
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
        if (NotOwnerAndNotAdmin(d)) return (null, Forbid());
        return (d, null);
    }

    private static int NN(long v) => v < 0 ? 0 : v > int.MaxValue ? int.MaxValue : (int)v;

    private static bool TryI32(JsonElement e, string a, string b, out int v)
    {
        v = 0;
        return (e.TryGetProperty(a, out var p) && p.TryGetInt32(out v))
            || (e.TryGetProperty(b, out p) && p.TryGetInt32(out v));
    }

    private static bool TryGetProperty(JsonElement obj, string camelName, string pascalName, out JsonElement value)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (obj.TryGetProperty(camelName, out value)) return true;
        if (obj.TryGetProperty(pascalName, out value)) return true;
        value = default;
        return false;
    }

    // -----------------------------
    // Core: Decks (single source of truth)
    // -----------------------------

    private async Task<(Paged<DeckDto>? Page, ActionResult? Error)> ListUserDecksCore(
        int userId,
        string? game,
        string? name,
        bool? hasCards,
        int page,
        int pageSize)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return (null, NotFound("User not found."));

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 50;

        var ct = HttpContext.RequestAborted;

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

        if (hasCards == true)
        {
            query = query.Where(d => _db.DeckCards.Any(dc => dc.DeckId == d.Id));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<DeckDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var paged = new Paged<DeckDto>(items, total, page, pageSize);
        return (paged, null);
    }

    private async Task<IActionResult> CreateDeckCore(int userId, CreateDeckRequest dto)
    {
        if (dto is null) return BadRequest();
        if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Game and Name required.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return NotFound("User not found.");

        var name = dto.Name.Trim();
        var game = dto.Game.Trim();

        var duplicate = await _db.Decks.AnyAsync(x =>
            x.UserId == userId &&
            EF.Functions.Collate(x.Game, "NOCASE") == game &&
            EF.Functions.Collate(x.Name, "NOCASE") == name);
        if (duplicate)
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
        if (NotOwnerAndNotAdmin(d)) return Forbid();
        return Ok(_mapper.Map<DeckResponse>(d));
    }

    private async Task<IActionResult> UpdateDeckCore(int deckId, UpdateDeckRequest dto)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Game) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Game and Name required.");

        var targetName = dto.Name.Trim();
        var targetGame = dto.Game.Trim();
        var nameInUse = await _db.Decks.AnyAsync(x =>
            x.UserId == d.UserId &&
            x.Id != d.Id &&
            EF.Functions.Collate(x.Game, "NOCASE") == targetGame &&
            EF.Functions.Collate(x.Name, "NOCASE") == targetName);
        if (nameInUse)
            return Conflict("Duplicate deck name for user.");

        _mapper.Map(dto, d);
        d.Game = targetGame;
        d.Name = targetName;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> PatchDeckForUserAsync(int userId, int deckId, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");

        var deck = await _db.Decks.FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);
        if (deck is null) return NotFound();

        return await ApplyDeckPatchAsync(deck, patch);
    }

    private async Task<IActionResult> ApplyDeckPatchAsync(Deck deck, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");

        var ownerId = deck.UserId;
        var currentName = deck.Name ?? string.Empty;
        var changed = false;

        string? requestedGame = null;
        if (TryGetProperty(patch, "game", "Game", out var targetGameProp) && targetGameProp.ValueKind == JsonValueKind.String)
        {
            var newGameRaw = targetGameProp.GetString();
            var newGame = newGameRaw?.Trim();
            if (!string.IsNullOrWhiteSpace(newGame))
            {
                requestedGame = newGame;
            }
        }

        var targetGameForNameCheck = requestedGame ?? deck.Game;

        if (TryGetProperty(patch, "name", "Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            var requestedName = (nameProp.GetString() ?? string.Empty).Trim();
            if (!string.Equals(currentName, requestedName, StringComparison.Ordinal))
            {
                // Case-insensitive, SQL-translatable check using SQLite NOCASE collation
                var exists = await _db.Decks.AnyAsync(d =>
                    d.UserId == ownerId &&
                    d.Id != deck.Id &&
                    EF.Functions.Collate(d.Game, "NOCASE") == targetGameForNameCheck &&
                    EF.Functions.Collate(d.Name, "NOCASE") == requestedName);

                if (exists) return Conflict("A deck with this name already exists.");

                deck.Name = requestedName;
                currentName = requestedName;
                changed = true;
            }
        }

        if (TryGetProperty(patch, "description", "Description", out var descProp))
        {
            if (descProp.ValueKind == JsonValueKind.Null)
            {
                if (deck.Description != null)
                {
                    deck.Description = null;
                    changed = true;
                }
            }
            else if (descProp.ValueKind == JsonValueKind.String)
            {
                var requestedDescription = descProp.GetString();
                if (!string.Equals(deck.Description, requestedDescription, StringComparison.Ordinal))
                {
                    deck.Description = requestedDescription;
                    changed = true;
                }
            }
        }

        if (requestedGame is not null)
        {
            if (!string.Equals(deck.Game, requestedGame, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedName = (deck.Name ?? string.Empty).Trim().ToLower();
                var collision = await _db.Decks.AnyAsync(d =>
                    d.UserId == ownerId &&
                    d.Id != deck.Id &&
                    string.Equals(d.Game, requestedGame, StringComparison.OrdinalIgnoreCase) &&
                    d.Name.ToLower() == normalizedName);
                if (collision) return Conflict("Name already exists in the target game.");
            }

            if (!string.Equals(deck.Game, requestedGame, StringComparison.Ordinal))
            {
                deck.Game = requestedGame;
                changed = true;
            }
        }

        if (!changed) return NoContent();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> DeleteDeckCore(int deckId)
    {
        var d = await _db.Decks.FirstOrDefaultAsync(x => x.Id == deckId);
        if (d is null) return NotFound();
        if (NotOwnerAndNotAdmin(d)) return Forbid();
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

        if (updates.ValueKind != JsonValueKind.Object) return BadRequest("JSON object required.");

        var dc = await _db.DeckCards.FirstOrDefaultAsync(x => x.DeckId == deckId && x.CardPrintingId == cardPrintingId);
        if (dc is null)
        {
            var cp = await _db.CardPrintings.Include(x => x.Card).FirstOrDefaultAsync(x => x.Id == cardPrintingId);
            if (cp is null) return NotFound("CardPrinting not found.");
            if (!string.Equals(deck!.Game, cp.Card.Game, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Card game does not match deck game.");

            dc = new DeckCard
            {
                DeckId = deckId,
                CardPrintingId = cardPrintingId,
                QuantityInDeck = 0,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 0
            };
            _db.DeckCards.Add(dc);
        }

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
            var (available, availableWithProxy) = CardAvailabilityHelper.Calculate(owned, proxy, assigned);
            if (!includeProxies)
            {
                availableWithProxy = available;
            }

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
    public async Task<ActionResult<Paged<DeckDto>>> GetUserDecks(
        int userId,
        [FromQuery] string? game = null,
        [FromQuery] string? name = null,
        [FromQuery] bool? hasCards = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (UserMismatch(userId)) return Forbid();
        var (pagedResult, error) = await ListUserDecksCore(userId, game, name, hasCards, page, pageSize);
        if (error != null) return error;
        return Ok(pagedResult);
    }

    // POST /api/user/{userId}/deck
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateDeck(int userId, [FromBody] CreateDeckRequest dto)
    {
        if (UserMismatch(userId)) return Forbid();
        return await CreateDeckCore(userId, dto);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int userId, int id, [FromBody] JsonElement patch)
    {
        if (UserMismatch(userId)) return Forbid();
        return await PatchDeckForUserAsync(userId, id, patch);
    }

    // -----------------------------------------
    // Auth-derived aliases (preferred)
    // -----------------------------------------

    // GET /api/decks  (list current user's decks)
    [HttpGet("/api/decks")] // alias, optional
    public async Task<ActionResult<Paged<DeckDto>>> GetMyDecks(
        [FromQuery] string? game = null,
        [FromQuery] string? name = null,
        [FromQuery] bool? hasCards = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        var (pageResult, error) = await ListUserDecksCore(uid, game, name, hasCards, page, pageSize);
        if (error != null) return error;
        return Ok(pageResult);
    }

    // GET /api/deck  (legacy alias returning bare array)
    [HttpGet("/api/deck")]
    public async Task<ActionResult<List<DeckDto>>> GetMyDecksAlias(
        [FromQuery] string? game = null,
        [FromQuery] string? name = null,
        [FromQuery] bool? hasCards = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        var (pageResult, error) = await ListUserDecksCore(uid, game, name, hasCards, page, pageSize);
        if (error != null) return error;
        return Ok(pageResult!.Items.ToList());
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
    public async Task<IActionResult> PatchDeck(int deckId, [FromBody] JsonElement patch)
    {
        var (deck, error) = await GetDeckForCaller(deckId);
        if (error != null) return error;
        return await ApplyDeckPatchAsync(deck!, patch);
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
    public async Task<IActionResult> PatchDeckCardQuantities(int deckId, int cardPrintingId, [FromBody] JsonElement patch)
        => await PatchDeckCardQuantitiesCore(deckId, cardPrintingId, patch);

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
