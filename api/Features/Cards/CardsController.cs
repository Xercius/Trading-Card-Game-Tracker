using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
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
/// - Managing card printing data
/// - Virtualized pagination for efficient large dataset handling
/// </remarks>
[ApiController]
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
    /// <param name="type">Optional comma-separated list of associated card types to filter.</param>
    /// <param name="arena">Optional comma-separated list of associated card arenas to filter.</param>
    /// <param name="aspect">Optional comma-separated list of associated card aspects to filter.</param>
    /// <param name="aspectMethod">Filtering method for aspects ('or', 'and', 'exclude').</param>
    /// <param name="trait">Optional comma-separated list of associated card traits to filter.</param>
    /// <param name="traitMethod">Filtering method for traits ('or', 'and', 'exclude').</param>
    /// <param name="keyword">Optional comma-separated list of associated card keywords to filter.</param>
    /// <param name="keywordMethod">Filtering method for keywords ('or', 'and', 'exclude').</param>
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
        [FromQuery] string? type,
        [FromQuery] string? arena = null,
        [FromQuery] string? aspect = null,
        [FromQuery] string? aspectMethod = null,
        [FromQuery] string? trait = null,
        [FromQuery] string? traitMethod = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? keywordMethod = null,
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
        var types = CsvUtils.Parse(type);
        var arenas = CsvUtils.Parse(arena);
        var aspects = CsvUtils.ParsePreservingDuplicates(aspect);
        var traits = CsvUtils.Parse(trait);
        var keywords = CsvUtils.Parse(keyword);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameIds = await _db.Games
            .Where(g => normalizedGames.Contains(g.NameNormalized))
            .Select(g => g.Id)
            .ToListAsync(ct);

        var query = _db.Cards
            .AsNoTracking()
            .ApplyCardSearchFilters(q, games, sets, rarities, types, gameIds, arenas, aspects, traits, aspectMethod, traitMethod, keywords, keywordMethod)
            .OrderBy(c => c.Game == null ? string.Empty : c.Game.Name)
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
        var cardIds = items.Select(item => item.CardId).ToList();
        if (cardIds.Count > 0)
        {
            var typeAssociations = await _db.CardCardTypes
                .AsNoTracking()
                .Where(association => cardIds.Contains(association.CardId))
                .Select(association => new { association.CardId, Name = association.CardType.Name })
                .ToListAsync(ct);
            var aspectAssociations = await _db.CardAspects
                .AsNoTracking()
                .Where(association => cardIds.Contains(association.CardId))
                .Select(association => new
                {
                    association.CardId,
                    Response = new CardAspectResponse(association.Aspect.Name, association.Count)
                })
                .ToListAsync(ct);
            var cardTypesById = typeAssociations
                .GroupBy(type => type.CardId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(type => type.Name)
                        .Distinct().OrderBy(name => name).ToList());
            var aspectsById = aspectAssociations
                .GroupBy(aspect => aspect.CardId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CardAspectResponse>)group.Select(aspect => aspect.Response)
                        .OrderBy(aspect => aspect.Name).ToList());
            foreach (var item in items)
            {
                item.CardTypes = cardTypesById.GetValueOrDefault(item.CardId) ?? Array.Empty<string>();
                item.Aspects = aspectsById.GetValueOrDefault(item.CardId) ?? Array.Empty<CardAspectResponse>();
            }
        }

        var nextSkip = items.Count < take ? (int?)null : skip + take;

        return Ok(new CardListPageResponse
        {
            Items = items,
            Total = total,
            NextSkip = nextSkip
        });
    }


    /// <summary>
    /// Core implementation for searching and listing cards with traditional pagination.
    /// </summary>
    /// <param name="game">Optional game name to filter cards.</param>
    /// <param name="name">Optional name pattern to search for (case-insensitive, partial match).</param>
    /// <param name="set">Optional comma-separated set filter.</param>
    /// <param name="rarity">Optional comma-separated rarity filter.</param>
    /// <param name="type">Optional comma-separated associated card-type filter.</param>
    /// <param name="arena">Optional comma-separated associated card-arena filter.</param>
    /// <param name="aspect">Optional comma-separated associated card-aspect filter.</param>
    /// <param name="aspectMethod">Filtering method for aspects ('or', 'and', 'exclude').</param>
    /// <param name="trait">Optional comma-separated associated card-trait filter.</param>
    /// <param name="traitMethod">Filtering method for traits ('or', 'and', 'exclude').</param>
    /// <param name="keyword">Optional comma-separated associated card-keyword filter.</param>
    /// <param name="keywordMethod">Filtering method for keywords ('or', 'and', 'exclude').</param>
    /// <param name="includePrintings">If true, includes all printings for each card in the response.</param>
    /// <param name="page">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of results per page (default: 50, max: 200).</param>
    /// <returns>
    /// A paged result containing either <see cref="CardListItemResponse"/> (when includePrintings is false)
    /// or <see cref="CardDetailResponse"/> (when includePrintings is true).
    /// </returns>
    private async Task<IActionResult> ListCardsCore(
        string? game,
        string? name,
        string? set,
        string? rarity,
        string? type,
        string? arena,
        string? aspect,
        string? trait,
        bool includePrintings,
        int page, int pageSize,
        string? aspectMethod = null,
        string? traitMethod = null,
        string? keyword = null,
        string? keywordMethod = null)
    {
        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0 || pageSize > SearchMaxPageSize) pageSize = SearchDefaultPageSize;

        var q = _db.Cards.AsNoTracking().AsQueryable();
        var ct = HttpContext.RequestAborted;
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);
        var types = CsvUtils.Parse(type);
        var arenas = CsvUtils.Parse(arena);
        var aspects = CsvUtils.ParsePreservingDuplicates(aspect);
        var traits = CsvUtils.Parse(trait);
        var keywords = CsvUtils.Parse(keyword);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameIds = await _db.Games
            .Where(candidate => normalizedGames.Contains(candidate.NameNormalized))
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        q = q.ApplyCardSearchFilters(null, games, sets, rarities, types, gameIds, arenas, aspects, traits, aspectMethod, traitMethod, keywords, keywordMethod);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{name.Trim()}%";
            q = q.Where(c => EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern));
        }

        var total = await q.CountAsync(ct);

        q = q.OrderBy(c => c.Name).ThenBy(c => c.Id);

        var cards = await q
            .AsSplitQuery()
            .Include(card => card.Game)
            .Include(card => card.CardTypes).ThenInclude(type => type.CardType)
            .Include(card => card.CardAspects).ThenInclude(aspect => aspect.Aspect)
            .Include(card => card.CardArenas).ThenInclude(arena => arena.Arena)
            .Include(card => card.CardTraits).ThenInclude(trait => trait.Trait)
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
            .Include(cp => cp.CatalogSet)
            .Include(cp => cp.RarityLookup)
            .Include(cp => cp.Treatment)
            .OrderBy(cp => cp.CatalogSet == null ? string.Empty : cp.CatalogSet.Code).ThenBy(cp => cp.Number)
            .ToListAsync(ct);

        var map = printings
            .GroupBy(p => p.CardId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var detailed = cards.Select(c => BuildCardDetailResponse(
            c,
            map.TryGetValue(c.Id, out var list)
                ? list.Select(MapPrintingResponse).ToList()
                : new List<CardPrintingResponse>())).ToList();

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
        var c = await _db.Cards
            .AsNoTracking()
            .Include(card => card.CardTypes).ThenInclude(type => type.CardType)
            .Include(card => card.Game)
            .Include(card => card.CardAspects).ThenInclude(aspect => aspect.Aspect)
            .Include(card => card.CardArenas).ThenInclude(arena => arena.Arena)
            .Include(card => card.CardTraits).ThenInclude(trait => trait.Trait)
            .FirstOrDefaultAsync(x => x.Id == cardId);
        if (c is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card {cardId} was not found.");
        }

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .Include(cp => cp.CatalogSet)
            .Include(cp => cp.RarityLookup)
            .Include(cp => cp.Treatment)
            .OrderBy(cp => cp.CatalogSet == null ? string.Empty : cp.CatalogSet.Code).ThenBy(cp => cp.Number)
            .ToListAsync();

        var dto = BuildCardDetailResponse(c, printings.Select(MapPrintingResponse).ToList());

        return Ok(dto);
    }

    private static CardDetailResponse BuildCardDetailResponse(Card c, List<CardPrintingResponse> printings)
        => new(
            c.Id,
            c.Name,
            c.Game?.Name ?? string.Empty,
            c.CardTypes.FirstOrDefault(type => type.SourceRole == "Type")?.CardType.Name ?? string.Empty,
            c.Text,
            printings,
            c.CardTypes.Select(type => type.CardType.Name).Distinct().OrderBy(name => name).ToList(),
            c.CardAspects.OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList(),
            c.CardTypes.FirstOrDefault(type => type.SourceRole == "Type2")?.CardType.Name,
            string.Join(", ", c.CardArenas.Select(a => a.Arena.Name).Distinct().OrderBy(n => n)),
            c.CardTraits.Select(trait => trait.Trait.Name).Distinct().OrderBy(name => name).ToList(),
            c.TextStyled,
            c.EpicAction,
            c.EpicActionStyled,
            c.DeployBox,
            c.DeployBoxStyled,
            c.Cost,
            c.Hp,
            c.Power,
            c.UpgradeHp,
            c.UpgradePower,
            c.Subtitle,
            c.Rules,
            c.RulesStyled);

    /// <summary>
    /// Core implementation for creating or updating a card printing record.
    /// Operation that performs upsert logic based on the presence of printing ID or set/number combination.
    /// </summary>
    /// <param name="dto">The printing data to create or update.</param>
    /// <returns>
    /// NoContent (204) on success,
    /// 404 Not Found if the specified card or printing ID doesn't exist,
    /// or 400 Bad Request with validation errors for invalid input.
    /// </returns>
    /// <remarks>
    /// If dto.Id is provided, updates the existing printing with that ID.
    /// If dto.Id is null, attempts to find an existing printing by CardId, Set, Number, and treatment.
    /// If no match is found, creates a new printing record.
    /// Set and Number are required when Id is not provided.
    /// </remarks>
    private async Task<IActionResult> UpsertPrintingCore(UpsertPrintingRequest dto)
    {
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

        var card = await _db.Cards.FindAsync(dto.CardId);
        if (card is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {dto.CardId} was not found.");
        }

        string? set = dto.Set?.Trim();
        string? number = dto.Number?.Trim();
        string? treatmentName = dto.Style?.Trim();

        CardPrinting? cp = null;

        if (dto.Id.HasValue)
        {
            cp = await _db.CardPrintings.Include(x => x.Treatment).Include(x => x.Card)
                .FirstOrDefaultAsync(x => x.Id == dto.Id.Value);
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

        }

        var setLookup = dto.Id.HasValue && dto.Set is null
            ? cp?.CatalogSet
            : await GetOrCreateSetAsync(cp?.Card.GameId ?? card.GameId, set);
        var rarityLookup = dto.Id.HasValue && dto.Rarity is null
            ? cp?.RarityLookup
            : await GetOrCreateRarityAsync(cp?.Card.GameId ?? card.GameId, dto.Rarity?.Trim());
        var treatment = dto.Id.HasValue && dto.Style is null
            ? null
            : await GetOrCreateTreatmentAsync(cp?.Card.GameId ?? card.GameId, treatmentName);

        if (!dto.Id.HasValue)
        {
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x =>
                x.CardId == dto.CardId &&
                setLookup != null && x.SetId == setLookup.Id &&
                x.Number == number &&
                treatment != null && x.TreatmentId == treatment.Id
            );
        }

        if (cp is null)
        {
            cp = new CardPrinting
            {
                CardId = dto.CardId,
                Number = number!,
                CatalogSet = setLookup,
                RarityLookup = rarityLookup,
                Treatment = treatment!,
                ImageUrl = dto.ImageUrl
            };
            _db.CardPrintings.Add(cp);
        }
        else
        {
            if (dto.Set is not null) cp.CatalogSet = setLookup;
            if (dto.Number is not null) cp.Number = number!;
            if (dto.Rarity is not null) cp.RarityLookup = rarityLookup;
            if (treatment is not null) cp.Treatment = treatment;
            if (dto.ImageUrlSet) cp.ImageUrl = dto.ImageUrl;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Core implementation for bulk importing multiple printing records for a specific card.
    /// Creates or updates printings in batch.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to add printings to.</param>
    /// <param name="items">Collection of printing data to import.</param>
    /// <returns>
    /// NoContent (204) on success,
    /// 404 Not Found if the specified card doesn't exist,
    /// or 400 Bad Request for invalid input.
    /// </returns>
    /// <remarks>
    /// For each item, if a printing with matching Set, Number, and treatment exists, it updates that printing.
    /// Otherwise, creates a new printing record. Items missing Set or Number are skipped silently.
    /// </remarks>
    private async Task<IActionResult> BulkImportPrintingsCore(int cardId, IEnumerable<UpsertPrintingRequest> items)
    {
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
            .Include(cp => cp.CatalogSet)
            .Include(cp => cp.RarityLookup)
            .Include(cp => cp.Treatment)
            .Where(cp => cp.CardId == cardId)
            .ToListAsync();
        var card = await _db.Cards.FindAsync(cardId);

        foreach (var dto in list)
        {
            var set = dto.Set?.Trim();
            var number = dto.Number?.Trim();

            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
                continue;

            var setLookup = await GetOrCreateSetAsync(card!.GameId, set);
            var rarityLookup = await GetOrCreateRarityAsync(card.GameId, dto.Rarity?.Trim());
            var treatment = await GetOrCreateTreatmentAsync(card!.GameId, dto.Style);
            var match = existing.FirstOrDefault(x =>
                setLookup != null && (setLookup.Id == 0
                    ? ReferenceEquals(x.CatalogSet, setLookup)
                    : x.SetId == setLookup.Id) &&
                x.Number == number &&
                (treatment.Id == 0
                    ? ReferenceEquals(x.Treatment, treatment)
                    : x.TreatmentId == treatment.Id)
            );

            if (match is null)
            {
                var created = new CardPrinting
                {
                    CardId = cardId,
                    Number = number!,
                    CatalogSet = setLookup,
                    RarityLookup = rarityLookup,
                    Treatment = treatment,
                    ImageUrl = dto.ImageUrl
                };
                _db.CardPrintings.Add(created);
                existing.Add(created);
            }
            else
            {
                if (dto.Rarity is not null) match.RarityLookup = rarityLookup;
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
    /// <param name="set">Optional comma-separated set filter.</param>
    /// <param name="rarity">Optional comma-separated rarity filter.</param>
    /// <param name="type">Optional comma-separated associated card-type filter.</param>
    /// <param name="arena">Optional comma-separated associated card-arena filter.</param>
    /// <param name="aspect">Optional comma-separated associated card-aspect filter.</param>
    /// <param name="aspectMethod">Filtering method for aspects ('or', 'and', 'exclude').</param>
    /// <param name="trait">Optional comma-separated associated card-trait filter.</param>
    /// <param name="traitMethod">Filtering method for traits ('or', 'and', 'exclude').</param>
    /// <param name="keyword">Optional comma-separated associated card-keyword filter.</param>
    /// <param name="keywordMethod">Filtering method for keywords ('or', 'and', 'exclude').</param>
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
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? type,
        [FromQuery] string? arena = null,
        [FromQuery] string? aspect = null,
        [FromQuery] string? aspectMethod = null,
        [FromQuery] string? trait = null,
        [FromQuery] string? traitMethod = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? keywordMethod = null,
        [FromQuery] bool includePrintings = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => await ListCardsCore(game, name, set, rarity, type, arena, aspect, trait, includePrintings, page, pageSize, aspectMethod, traitMethod, keyword, keywordMethod);

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
    /// Retrieves all printing records for a specific card in the appropriate set order.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A list of <see cref="PrintingDto"/> objects representing each printing of the card.</returns>
    /// <response code="200">Returns the list of printings.</response>
    /// <response code="404">Card with the specified ID was not found.</response>
    [HttpGet("{cardId:int}/printings")]
    public async Task<ActionResult<IReadOnlyList<PrintingDto>>> GetCardPrintings(int cardId, CancellationToken ct)
    {
        var swuGameIds = await _db.Games
            .Where(game => game.NameNormalized == "star wars unlimited")
            .Select(game => game.Id)
            .ToListAsync(ct);
        var cardIsSwu = await _db.Cards
            .AsNoTracking()
            .Where(card => card.Id == cardId)
            .Select(card => (bool?)(
                swuGameIds.Contains(card.GameId ?? 0) ||
                card.CardTypes.Any(type => swuGameIds.Contains(type.CardType.GameId)) ||
                card.Printings.Any(printing => printing.CatalogSet != null &&
                    swuGameIds.Contains(printing.CatalogSet.GameId))))
            .FirstOrDefaultAsync(ct);
        if (cardIsSwu is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {cardId} was not found.");
        }

        var printingsQuery = _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .Include(cp => cp.CatalogSet)
            .Include(cp => cp.RarityLookup)
            .Include(cp => cp.Treatment)
            .Include(cp => cp.Card).ThenInclude(card => card.Game)
            .Include(cp => cp.Card).ThenInclude(card => card.CardTypes).ThenInclude(type => type.CardType)
            .Include(cp => cp.Card).ThenInclude(card => card.CardAspects).ThenInclude(aspect => aspect.Aspect);
        var orderedPrintings = cardIsSwu.Value
            ? printingsQuery
                .OrderBy(cp => cp.CatalogSet == null || cp.CatalogSet.ReleasedAt == null)
                .ThenBy(cp => cp.CatalogSet == null ? null : cp.CatalogSet.ReleasedAt)
                .ThenBy(cp => cp.CatalogSet == null ? string.Empty : cp.CatalogSet.Name ?? cp.CatalogSet.Code)
                .ThenBy(cp => cp.Number)
                .ThenBy(cp => cp.Id)
            : printingsQuery
                .OrderBy(cp => cp.CatalogSet == null ? string.Empty : cp.CatalogSet.Name ?? cp.CatalogSet.Code)
                .ThenBy(cp => cp.Number)
                .ThenBy(cp => cp.Id);
        var printings = await orderedPrintings
            .ToListAsync(ct);
        var rows = printings.Select(ToPrintingDto).ToList();

        return Ok(rows);
    }

    private static CardPrintingResponse MapPrintingResponse(CardPrinting printing)
        => new(
            printing.Id,
            printing.CatalogSet == null ? string.Empty : printing.CatalogSet.Name ?? printing.CatalogSet.Code,
            printing.Number,
            printing.RarityLookup?.Name ?? string.Empty,
            printing.Treatment?.Name ?? "Standard",
            printing.ImageUrl,
            printing.CatalogSet?.Code,
            GetReleaseDate(printing),
            printing.BackImageUrl,
            GetOrientation(printing.ArtFrontHorizontal),
            GetOrientation(printing.ArtBackHorizontal),
            printing.Artist);

    private static PrintingDto ToPrintingDto(CardPrinting printing)
        => new(
            printing.Id,
            printing.CatalogSet == null ? string.Empty : printing.CatalogSet.Name ?? printing.CatalogSet.Code,
            printing.CatalogSet?.Code,
            printing.Number,
            printing.RarityLookup?.Name ?? string.Empty,
            printing.ImageUrl ?? PlaceholderCardImage,
            printing.CardId,
            printing.Card.Name,
            printing.Card.Game?.Name ?? string.Empty)
        {
            Style = printing.Treatment?.Name ?? "Standard",
            ReleaseDate = GetReleaseDate(printing),
            BackImageUrl = printing.BackImageUrl,
            ImageOrientation = GetOrientation(printing.ArtFrontHorizontal),
            BackImageOrientation = GetOrientation(printing.ArtBackHorizontal),
            CardTypes = printing.Card.CardTypes.Select(type => type.CardType.Name)
                .Distinct().OrderBy(name => name).ToList(),
            Aspects = printing.Card.CardAspects.OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList()
        };

    private async Task<Treatment> GetOrCreateTreatmentAsync(int? gameId, string? name)
    {
        var treatmentName = string.IsNullOrWhiteSpace(name) ? "Standard" : name.Trim();
        var normalizedName = SqliteCaseNormalizer.Normalize(treatmentName);
        var game = gameId.HasValue
            ? _db.Games.Local.FirstOrDefault(candidate => candidate.Id == gameId.Value)
                ?? await _db.Games.FindAsync(gameId.Value)
            : null;
        if (game is null)
            throw new InvalidOperationException("The card must have a game before adding a printing.");

        var treatment = _db.Treatments.Local.FirstOrDefault(candidate =>
            (candidate.Game == game || (game.Id != 0 && candidate.GameId == game.Id))
            && SqliteCaseNormalizer.Normalize(candidate.Name) == normalizedName);
        if (treatment is null && game.Id != 0)
        {
            treatment = await _db.Treatments.FirstOrDefaultAsync(candidate =>
                candidate.GameId == game.Id && candidate.NameNormalized == normalizedName);
        }
        if (treatment is not null)
            return treatment;

        treatment = new Treatment { Game = game, Name = treatmentName };
        _db.Treatments.Add(treatment);
        return treatment;
    }

    private static string? GetOrientation(bool? horizontal)
        => horizontal switch { true => "horizontal", false => "vertical", _ => null };

    private static DateOnly? GetReleaseDate(CardPrinting printing)
        => printing.CatalogSet?.ReleasedAt
            ?? (printing.ApiPublishedAt is { } publishedAt
                ? DateOnly.FromDateTime(publishedAt.UtcDateTime)
                : null);

    private async Task<Set?> GetOrCreateSetAsync(int? gameId, string? code)
    {
        if (gameId is null || string.IsNullOrWhiteSpace(code))
            return null;

        code = code.Trim();
        var set = _db.Sets.Local.FirstOrDefault(candidate =>
                candidate.GameId == gameId && candidate.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? await _db.Sets.FirstOrDefaultAsync(candidate =>
                candidate.GameId == gameId
                && EF.Functions.Collate(candidate.Code, "NOCASE") == code);
        if (set is not null)
            return set;

        set = new Set { GameId = gameId.Value, Code = code, LastSyncedAt = DateTimeOffset.UnixEpoch };
        _db.Sets.Add(set);
        return set;
    }

    private async Task<Rarity?> GetOrCreateRarityAsync(int? gameId, string? name)
    {
        if (gameId is null || string.IsNullOrWhiteSpace(name))
            return null;

        name = name.Trim();
        var rarity = _db.Rarities.Local.FirstOrDefault(candidate =>
                candidate.GameId == gameId && candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? await _db.Rarities.FirstOrDefaultAsync(candidate =>
                candidate.GameId == gameId
                && EF.Functions.Collate(candidate.Name, "NOCASE") == name);
        if (rarity is not null)
            return rarity;

        rarity = new Rarity { GameId = gameId.Value, Name = name };
        _db.Rarities.Add(rarity);
        return rarity;
    }

    /// <summary>
    /// Creates or updates a card printing record. Admin-only endpoint.
    /// </summary>
    /// <param name="dto">The printing data containing card ID, set, number, rarity, style, and image URL.</param>
    /// <returns>NoContent (204) on success.</returns>
    /// <response code="204">Printing was successfully created or updated.</response>
    /// <response code="400">Invalid request data or validation errors.</response>
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
    /// Bulk imports multiple printing records for a specific card.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to add printings to.</param>
    /// <param name="items">Collection of printing data to import.</param>
    /// <returns>NoContent (204) on success.</returns>
    /// <response code="204">Printings were successfully imported.</response>
    /// <response code="400">Invalid request data or validation errors.</response>
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
