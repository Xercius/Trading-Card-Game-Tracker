using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

/// <summary>
/// Controller for managing trading card data and card printings.
/// Handles routes under /api/cards for querying, searching, and managing card information.
/// </summary>
/// <remarks>
/// This controller provides endpoints for:
/// - Listing and searching cards across all supported games
/// - Retrieving detailed card information including all printings
/// - Managing card printing data (admin-only operations)
/// - Virtualized pagination for efficient large dataset handling
/// All endpoints require authentication via the [Authorize] attribute.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/cards")] // plural route
public class CardsController : ControllerBase
{
    private const string PlaceholderCardImage = "/images/placeholders/card-3x4.png";
    private const int VirtualizedDefaultTake = 60;
    private const int VirtualizedMaxTake = 200;
    private const int MinimumSkip = 0;
    private const int DefaultPageNumber = 1;
    private const int SearchDefaultPageSize = 50;
    private const int SearchMaxPageSize = 200;

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CardsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    /// <summary>
    /// Lists cards with virtualized pagination for efficient scrolling through large datasets.
    /// </summary>
    /// <param name="q">Optional search query to filter cards by name.</param>
    /// <param name="game">Optional comma-separated list of game names to filter (e.g., "Magic,Lorcana").</param>
    /// <param name="set">Optional comma-separated list of set names to filter.</param>
    /// <param name="rarity">Optional comma-separated list of rarities to filter.</param>
    /// <param name="skip">Number of records to skip for pagination (default: 0).</param>
    /// <param name="take">Number of records to take per page (default: 60, max: 200).</param>
    /// <param name="includeTotal">Whether to include the total count of matching cards (default: false).</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A <see cref="CardListPageResponse"/> containing card summaries, optional total count, and next skip value.</returns>
    /// <response code="200">Returns the requested page of cards.</response>
    // GET /api/cards?q=&game=Magic,Lorcana&skip=0&take=60&includeTotal=false
    [HttpGet]
    public async Task<ActionResult<CardListPageResponse>> ListCardsVirtualized(
        [FromQuery] string? q,
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 60,
        [FromQuery] bool includeTotal = false,
        CancellationToken ct = default)
    {
        take = take <= 0 ? VirtualizedDefaultTake : Math.Min(take, VirtualizedMaxTake);
        if (skip < 0) skip = MinimumSkip;

        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);

        var query = _db.Cards
            .AsNoTracking()
            .ApplyCardSearchFilters(q, games, sets, rarities)
            .OrderBy(c => c.Game)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id);

        int? total = null;
        if (includeTotal)
        {
            total = await query.CountAsync(ct);
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .SelectCardSummaries(PlaceholderCardImage)
            .ToListAsync(ct);

        var nextSkip = items.Count < take ? (int?)null : skip + take;

        return Ok(new CardListPageResponse
        {
            Items = items,
            Total = total,
            NextSkip = nextSkip
        });
    }

    /// <summary>
    /// Helper method to check if the current user has administrator privileges.
    /// </summary>
    /// <returns>True if the user is not an admin or not authenticated; false if the user is an admin.</returns>
    // CSV parsing logic moved to CsvUtils in api.Shared
    private bool NotAdmin()
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || !me.IsAdmin;
    }

    /// <summary>
    /// Core implementation for searching and listing cards with traditional pagination.
    /// </summary>
    /// <param name="game">Optional game name to filter cards.</param>
    /// <param name="name">Optional name pattern to search for (case-insensitive, partial match).</param>
    /// <param name="includePrintings">If true, includes all printings for each card in the response.</param>
    /// <param name="page">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of results per page (default: 50, max: 200).</param>
    /// <returns>
    /// A paged result containing either <see cref="CardListItemResponse"/> (when includePrintings is false)
    /// or <see cref="CardDetailResponse"/> (when includePrintings is true).
    /// </returns>
    private async Task<IActionResult> ListCardsCore(
        string? game, string? name,
        bool includePrintings,
        int page, int pageSize)
    {
        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0 || pageSize > SearchMaxPageSize) pageSize = SearchDefaultPageSize;

        var q = _db.Cards.AsNoTracking().AsQueryable();
        var ct = HttpContext.RequestAborted;

        if (!string.IsNullOrWhiteSpace(game))
        {
            var normalized = game.Trim().ToLowerInvariant();
            q = q.Where(c => EF.Property<string>(c, "GameNorm") == normalized);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{name.Trim()}%";
            q = q.Where(c => EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern));
        }

        var total = await q.CountAsync(ct);

        q = q.OrderBy(c => c.Name).ThenBy(c => c.Id);

        var cards = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (!includePrintings)
        {
            var rows = _mapper.Map<List<CardListItemResponse>>(cards);
            return Ok(new Paged<CardListItemResponse>(rows, total, page, pageSize));
        }

        var cardIds = cards.Select(c => c.Id).ToList();

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cardIds.Contains(cp.CardId))
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync(ct);

        var map = printings
            .GroupBy(p => p.CardId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var detailed = cards.Select(c => new CardDetailResponse(
            c.Id,
            c.Name,
            c.Game,
            c.CardType,
            c.Description,
            map.TryGetValue(c.Id, out var list)
                ? _mapper.Map<List<CardPrintingResponse>>(list)
                : new List<CardPrintingResponse>()
        )).ToList();

        return Ok(new Paged<CardDetailResponse>(detailed, total, page, pageSize));
    }

    /// <summary>
    /// Core implementation for retrieving a single card with all its printings.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to retrieve.</param>
    /// <returns>
    /// A <see cref="CardDetailResponse"/> containing card details and all associated printings,
    /// or a 404 Problem Details response if the card is not found.
    /// </returns>
    private async Task<IActionResult> GetCardCore(int cardId)
    {
        var c = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cardId);
        if (c is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card {cardId} was not found.");
        }

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync();

        var dto = new CardDetailResponse(
            c.Id,
            c.Name,
            c.Game,
            c.CardType,
            c.Description,
            _mapper.Map<List<CardPrintingResponse>>(printings)
        );

        return Ok(dto);
    }

    /// <summary>
    /// Core implementation for creating or updating a card printing record.
    /// Admin-only operation that performs upsert logic based on the presence of printing ID or set/number combination.
    /// </summary>
    /// <param name="dto">The printing data to create or update.</param>
    /// <returns>
    /// NoContent (204) on success,
    /// 403 Forbidden if user is not an admin,
    /// 404 Not Found if the specified card or printing ID doesn't exist,
    /// or 400 Bad Request with validation errors for invalid input.
    /// </returns>
    /// <remarks>
    /// If dto.Id is provided, updates the existing printing with that ID.
    /// If dto.Id is null, attempts to find an existing printing by CardId, Set, Number, and Style.
    /// If no match is found, creates a new printing record.
    /// Set and Number are required when Id is not provided.
    /// </remarks>
    private async Task<IActionResult> UpsertPrintingCore(UpsertPrintingRequest dto)
    {
        if (NotAdmin())
        {
            return this.CreateProblem(StatusCodes.Status403Forbidden, detail: "Admin privileges are required to modify printings.");
        }

        if (dto is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "Printing payload is required." }
                });
        }

        if (dto.CardId <= 0)
        {
            return this.CreateValidationProblem(nameof(dto.CardId), "CardId must be provided.");
        }

        if (await _db.Cards.FindAsync(dto.CardId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {dto.CardId} was not found.");
        }

        string? set = dto.Set?.Trim();
        string? number = dto.Number?.Trim();
        string? style = dto.Style?.Trim();

        CardPrinting? cp = null;

        if (dto.Id.HasValue)
        {
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x => x.Id == dto.Id.Value);
            if (cp is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"Printing {dto.Id.Value} was not found.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
            {
                var errors = new Dictionary<string, string[]>();

                AddRequiredFieldError(errors, set, nameof(dto.Set), "Set is required when Id is omitted.");
                AddRequiredFieldError(errors, number, nameof(dto.Number), "Number is required when Id is omitted.");

                return this.CreateValidationProblem(errors);
            }

            cp = await _db.CardPrintings.FirstOrDefaultAsync(x =>
                x.CardId == dto.CardId &&
                x.Set == set &&
                x.Number == number &&
                (x.Style ?? "") == (style ?? "")
            );
        }

        if (cp is null)
        {
            cp = new CardPrinting
            {
                CardId = dto.CardId,
                Set = set!,
                Number = number!,
                Rarity = dto.Rarity?.Trim() ?? "",
                Style = style ?? "",
                ImageUrl = dto.ImageUrl
            };
            _db.CardPrintings.Add(cp);
        }
        else
        {
            if (dto.Set is not null) cp.Set = set!;
            if (dto.Number is not null) cp.Number = number!;
            if (dto.Rarity is not null) cp.Rarity = dto.Rarity.Trim();
            if (dto.Style is not null) cp.Style = style ?? "";
            if (dto.ImageUrlSet) cp.ImageUrl = dto.ImageUrl;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Core implementation for bulk importing multiple printing records for a specific card.
    /// Admin-only operation that creates or updates printings in batch.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to add printings to.</param>
    /// <param name="items">Collection of printing data to import.</param>
    /// <returns>
    /// NoContent (204) on success,
    /// 403 Forbidden if user is not an admin,
    /// 404 Not Found if the specified card doesn't exist,
    /// or 400 Bad Request for invalid input.
    /// </returns>
    /// <remarks>
    /// For each item, if a printing with matching Set, Number, and Style exists, it updates that printing.
    /// Otherwise, creates a new printing record. Items missing Set or Number are skipped silently.
    /// </remarks>
    private async Task<IActionResult> BulkImportPrintingsCore(int cardId, IEnumerable<UpsertPrintingRequest> items)
    {
        if (NotAdmin())
        {
            return this.CreateProblem(StatusCodes.Status403Forbidden, detail: "Admin privileges are required to bulk import printings.");
        }

        if (cardId <= 0)
        {
            return this.CreateValidationProblem(nameof(cardId), "cardId must be provided.");
        }

        if (items is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "Import payload is required." }
                });
        }

        if (await _db.Cards.FindAsync(cardId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {cardId} was not found.");
        }

        var list = items.ToList();
        if (list.Count == 0) return NoContent();

        var existing = await _db.CardPrintings
            .Where(cp => cp.CardId == cardId)
            .ToListAsync();

        foreach (var dto in list)
        {
            var set = dto.Set?.Trim();
            var number = dto.Number?.Trim();
            var style = dto.Style?.Trim();

            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
                continue;

            var match = existing.FirstOrDefault(x =>
                x.Set == set &&
                x.Number == number &&
                (x.Style ?? "") == (style ?? "")
            );

            if (match is null)
            {
                var created = new CardPrinting
                {
                    CardId = cardId,
                    Set = set!,
                    Number = number!,
                    Rarity = dto.Rarity?.Trim() ?? "",
                    Style = style ?? "",
                    ImageUrl = dto.ImageUrl
                };
                _db.CardPrintings.Add(created);
                existing.Add(created);
            }
            else
            {
                if (dto.Rarity is not null) match.Rarity = dto.Rarity.Trim();
                if (dto.ImageUrlSet) match.ImageUrl = dto.ImageUrl;
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Endpoints

    /// <summary>
    /// Searches for cards with traditional page-based pagination.
    /// </summary>
    /// <param name="game">Optional game name to filter (case-insensitive).</param>
    /// <param name="name">Optional card name to search for (case-insensitive, partial match).</param>
    /// <param name="includePrintings">If true, includes all printings for each card (default: false).</param>
    /// <param name="page">Page number, starting from 1 (default: 1).</param>
    /// <param name="pageSize">Results per page (default: 50, max: 200).</param>
    /// <returns>
    /// A <see cref="Paged{T}"/> result containing card summaries or detailed cards with printings.
    /// </returns>
    /// <response code="200">Returns the requested page of search results.</response>
    [HttpGet("search")]
    public async Task<IActionResult> ListCards(
        [FromQuery] string? game,
        [FromQuery] string? name,
        [FromQuery] bool includePrintings = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => await ListCardsCore(game, name, includePrintings, page, pageSize);

    /// <summary>
    /// Retrieves detailed information for a specific card, including all its printings.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <returns>A <see cref="CardDetailResponse"/> with card details and all printings.</returns>
    /// <response code="200">Returns the card details.</response>
    /// <response code="404">Card with the specified ID was not found.</response>
    [HttpGet("{cardId:int}")]
    public async Task<IActionResult> GetCard(int cardId)
        => await GetCardCore(cardId);

    /// <summary>
    /// Retrieves all printing records for a specific card, ordered by set and number.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A list of <see cref="PrintingDto"/> objects representing each printing of the card.</returns>
    /// <response code="200">Returns the list of printings.</response>
    /// <response code="404">Card with the specified ID was not found.</response>
    [HttpGet("{cardId:int}/printings")]
    public async Task<ActionResult<IReadOnlyList<PrintingDto>>> GetCardPrintings(int cardId, CancellationToken ct)
    {
        var exists = await _db.Cards.AsNoTracking().AnyAsync(c => c.Id == cardId, ct);
        if (!exists)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {cardId} was not found.");
        }

        var rows = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .OrderBy(cp => cp.Set)
            .ThenBy(cp => cp.Number)
            .Select(cp => new PrintingDto(
                cp.Id,
                cp.Set,
                null,
                cp.Number,
                cp.Rarity,
                cp.ImageUrl ?? PlaceholderCardImage,
                cp.CardId,
                cp.Card.Name,
                cp.Card.Game
            ))
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>
    /// Creates or updates a card printing record. Admin-only endpoint.
    /// </summary>
    /// <param name="dto">The printing data containing card ID, set, number, rarity, style, and image URL.</param>
    /// <returns>NoContent (204) on success.</returns>
    /// <response code="204">Printing was successfully created or updated.</response>
    /// <response code="400">Invalid request data or validation errors.</response>
    /// <response code="403">User does not have admin privileges.</response>
    /// <response code="404">Card or printing with the specified ID was not found.</response>
    /// <remarks>
    /// If dto.Id is provided, updates the existing printing.
    /// If dto.Id is null, searches for existing printing by CardId, Set, Number, and Style.
    /// Creates a new printing if no match is found. Set and Number are required when Id is null.
    /// </remarks>
    [HttpPost("printing")]
    public async Task<IActionResult> UpsertPrinting([FromBody] UpsertPrintingRequest dto)
        => await UpsertPrintingCore(dto);

    /// <summary>
    /// Bulk imports multiple printing records for a specific card. Admin-only endpoint.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to add printings to.</param>
    /// <param name="items">Collection of printing data to import.</param>
    /// <returns>NoContent (204) on success.</returns>
    /// <response code="204">Printings were successfully imported.</response>
    /// <response code="400">Invalid request data or validation errors.</response>
    /// <response code="403">User does not have admin privileges.</response>
    /// <response code="404">Card with the specified ID was not found.</response>
    /// <remarks>
    /// For each printing, if a match with the same Set, Number, and Style exists, it updates that record.
    /// Otherwise, creates a new printing. Items without Set or Number are skipped.
    /// </remarks>
    [HttpPost("{cardId:int}/printings/import")]
    public async Task<IActionResult> BulkImportPrintings(int cardId, [FromBody] IEnumerable<UpsertPrintingRequest> items)
        => await BulkImportPrintingsCore(cardId, items);

    /// <summary>
    /// Helper method to add a validation error for a required field if it's missing or empty.
    /// </summary>
    /// <param name="errors">Dictionary to accumulate validation errors.</param>
    /// <param name="value">The field value to check.</param>
    /// <param name="fieldName">Name of the field for the error key.</param>
    /// <param name="message">Error message to display if the field is invalid.</param>
    private static void AddRequiredFieldError(IDictionary<string, string[]> errors, string? value, string fieldName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[fieldName] = new[] { message };
        }
    }
}
