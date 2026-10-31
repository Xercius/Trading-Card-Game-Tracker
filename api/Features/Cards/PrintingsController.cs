using api.Data;                 // your DbContext namespace
using api.Models;              // Card, CardPrinting
using api.Features.Cards.Dtos;
using api.Features._Common;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace api.Features.Cards;

/// <summary>
/// Controller for querying and filtering card printing records.
/// Handles routes under /api/cards/printings for listing printing information across all supported trading card games.
/// </summary>
/// <remarks>
/// This controller provides endpoints for:
/// - Listing card printings with optional filters for game, set, rarity, style, and free-text search
/// - Case-insensitive filtering using EF.Functions.Like with NOCASE collation for SQLite compatibility
/// - Paginated results sorted by set, number, and card name
/// - Projection to PrintingDto to minimize payload size and avoid N+1 queries
/// All endpoints are unauthenticated and support query parameter filtering.
/// </remarks>
[ApiController]
[Route("api/cards/printings")]
public sealed class PrintingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PrintingsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists card printing records with optional filters for game, set, rarity, style, and search terms.
    /// </summary>
    /// <param name="qp">Query parameters containing optional filters: Game, Set, Number, Rarity, Style, Q (search term), Page, PageSize.</param> 
    /// <param name="ct">Cancellation token for request cancellation.</param>
    /// <returns>
    /// 200 OK with a collection of <see cref="PrintingDto"/> matching the filter criteria.
    /// Returns the total matching record count in the X-Total-Count response header.
    /// Results are sorted by Set, Number, then Card Name (or Card Name when Sort=name) and paginated.
    /// </returns>
    /// <remarks>
    /// Performance considerations:
    /// - Uses EF.Functions.Collate with NOCASE for case-insensitive filtering that leverages SQLite collation indices
    /// - Projects to PrintingDto to minimize payload size and prevent N+1 queries by including Card navigation
    /// - Pagination defaults to page 1, page size 100 (clamped to max 500)
    /// - Search term (Q) performs LIKE match against card name, type, printing number, and set name
    /// </remarks>
    [HttpGet]
    // Suggest: rename 'qp' → 'queryParams'
    public async Task<ActionResult<IEnumerable<PrintingDto>>> Get([FromQuery] ListPrintingsQuery qp, CancellationToken ct)
    {
        // Build base query with eager loading of Card for name and game information
        var query = _db.Set<CardPrinting>()
            .AsNoTracking()
            .Include(p => p.Card).ThenInclude(card => card.Game)
            .Include(p => p.Treatment)
            .AsQueryable();

        // Filter by game(s) - supports comma-separated list via CsvUtils
        var games = CsvUtils.Parse(qp.Game); // Suggest: rename 'games' → 'gameNames'
        var requestedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameIds = await _db.Games
            .Where(game => requestedGames.Contains(game.NameNormalized))
            .Select(game => game.Id)
            .ToListAsync(ct);
        var swuGameIds = await _db.Games
            .Where(game => game.NameNormalized == "star wars unlimited")
            .Select(game => game.Id)
            .ToListAsync(ct);
        if (games.Count > 0)
        {
            var otherGameIds = gameIds.Where(gameId => !swuGameIds.Contains(gameId)).ToList();
            var includesSwu = requestedGames.Contains("star wars unlimited");
            query = query.Where(p =>
                p.Card.GameId.HasValue && otherGameIds.Contains(p.Card.GameId.Value)
                || includesSwu && (
                    p.CatalogSet != null && swuGameIds.Contains(p.CatalogSet.GameId)
                    || p.CatalogSet == null && (
                        p.Card.CardTypes.Any(type => swuGameIds.Contains(type.CardType.GameId))
                        || p.Card.GameId.HasValue && swuGameIds.Contains(p.Card.GameId.Value))));
        }

        // Filter by set(s) - supports comma-separated list
        var sets = CsvUtils.Parse(qp.Set); // Suggest: rename 'sets' → 'setNames'
        if (sets.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedSets = sets.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                p.CatalogSet != null
                    && (normalizedSets.Contains(EF.Functions.Collate(p.CatalogSet.Code, "NOCASE")) ||
                        p.CatalogSet.Name != null && normalizedSets.Contains(
                            EF.Functions.Collate(p.CatalogSet.Name, "NOCASE"))));
        }

        // Filter by exact printing number (case-sensitive)
        if (!string.IsNullOrWhiteSpace(qp.Number))
        {
            var number = qp.Number.Trim();
            query = query.Where(p => p.Number == number);
        }

        // Filter by rarity/rarities - supports comma-separated list
        var rarities = CsvUtils.Parse(qp.Rarity);
        if (rarities.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedRarities = rarities.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                p.RarityLookup != null
                    && normalizedRarities.Contains(EF.Functions.Collate(p.RarityLookup.Name, "NOCASE")));
        }

        var types = CsvUtils.Parse(qp.Type);
        if (types.Count > 0)
        {
            var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                p.Card.CardTypes.Any(type =>
                    normalizedTypes.Contains(EF.Functions.Collate(type.CardType.Name, "NOCASE"))));
        }

        var arenas = CsvUtils.Parse(qp.Arena);
        if (arenas.Count > 0)
        {
            var normalizedArenas = arenas.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                p.Card.CardArenas.Any(arena =>
                    normalizedArenas.Contains(EF.Functions.Collate(arena.Arena.Name, "NOCASE"))));
        }

        var aspects = CsvUtils.ParsePreservingDuplicates(qp.Aspect);
        query = query.ApplyAspectFilter(aspects, qp.AspectMethod);

        var traits = CsvUtils.Parse(qp.Trait);
        if (traits.Count > 0)
        {
            var normalizedTraits = traits.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                p.Card.CardTraits.Any(trait =>
                    normalizedTraits.Contains(EF.Functions.Collate(trait.Trait.Name, "NOCASE"))));
        }

        // Filter by style(s) - supports comma-separated list
        var styles = CsvUtils.Parse(qp.Style);
        if (styles.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedStyles = styles.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(printing => printing.Treatment != null
                && normalizedStyles.Contains(printing.Treatment.NameNormalized));
        }

        // Free-text search across card name, type, printing number, and set name
        if (!string.IsNullOrWhiteSpace(qp.Q))
        {
            var term = qp.Q.Trim(); // Suggest: rename 'term' → 'searchTerm'
            var normalizedTerm = SqliteCaseNormalizer.Normalize(term);
            var normalizedPattern = $"%{normalizedTerm}%";
            var pattern = $"%{term}%";
            // Use normalized columns where available for index-friendly case-insensitive wildcard search
            query = query.Where(p =>
                (p.Card.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Card.Name, "NOCASE"), pattern)) ||
                p.Card.CardTypes.Any(type => EF.Functions.Like(
                    EF.Functions.Collate(type.CardType.Name, "NOCASE"), pattern)) ||
                (p.Number != null && EF.Functions.Like(EF.Functions.Collate(p.Number, "NOCASE"), pattern)) ||
                (p.CatalogSet != null && p.CatalogSet.Name != null &&
                    EF.Functions.Like(EF.Functions.Collate(p.CatalogSet.Name, "NOCASE"), pattern)) ||
                (p.CatalogSet != null && p.CatalogSet.Code != null &&
                    EF.Functions.Like(EF.Functions.Collate(p.CatalogSet.Code, "NOCASE"), pattern)));
        }

        if (qp.UniqueCards)
        {
            var matchingCardIds = query.Select(p => p.CardId).Distinct();
            var candidates = _db.Set<CardPrinting>()
                .AsNoTracking()
                .Include(p => p.Card).ThenInclude(card => card.Game)
                .Include(p => p.Treatment);
            query = candidates
                .Where(p => matchingCardIds.Contains(p.CardId))
                .Where(p => p.Id == candidates.Where(candidate => candidate.CardId == p.CardId)
                    .OrderByDescending(candidate => !string.IsNullOrEmpty(candidate.ImageUrl))
                    .ThenByDescending(candidate => candidate.Treatment != null
                        && candidate.Treatment.NameNormalized == "standard")
                    .ThenBy(candidate => candidate.ApiCreatedAt == null)
                    .ThenBy(candidate => AppDbContext.SqliteJulianDay(candidate.ApiCreatedAt))
                    .ThenBy(candidate => candidate.CatalogSet == null || candidate.CatalogSet.ReleasedAt == null)
                    .ThenBy(candidate => candidate.CatalogSet == null
                        ? null
                        : candidate.CatalogSet.ReleasedAt)
                    .ThenBy(candidate => requestedGames.Contains("star wars unlimited")
                        ? candidate.CatalogSet == null ? string.Empty : candidate.CatalogSet.Code
                        : string.Empty)
                    .ThenBy(candidate => candidate.Number == null ? 0 : candidate.Number.Length)
                    .ThenBy(candidate => candidate.Number)
                    .ThenBy(candidate => candidate.Id)
                    .Select(candidate => candidate.Id)
                    .FirstOrDefault());
        }

        if (string.Equals(qp.Sort, "name", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(qp.Sort, "name_asc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(qp.Sort, "alpha", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(qp.Sort, "alphabetical", StringComparison.OrdinalIgnoreCase))
        {
            query = query
                .OrderBy(p => EF.Functions.Collate(p.Card.Name, "NOCASE"))
                .ThenBy(p => p.CatalogSet == null || p.CatalogSet.ReleasedAt == null)
                .ThenBy(p => p.CatalogSet == null ? null : p.CatalogSet.ReleasedAt)
                .ThenBy(p => p.CatalogSet == null ? string.Empty : p.CatalogSet.Code)
                .ThenBy(p => p.Number)
                .ThenBy(p => p.Id);
        }
        else if (string.Equals(qp.Sort, "name_desc", StringComparison.OrdinalIgnoreCase))
        {
            query = query
                .OrderByDescending(p => EF.Functions.Collate(p.Card.Name, "NOCASE"))
                .ThenBy(p => p.CatalogSet == null || p.CatalogSet.ReleasedAt == null)
                .ThenBy(p => p.CatalogSet == null ? null : p.CatalogSet.ReleasedAt)
                .ThenBy(p => p.CatalogSet == null ? string.Empty : p.CatalogSet.Code)
                .ThenBy(p => p.Number)
                .ThenBy(p => p.Id);
        }
        // SWU cards use the verified set release date; generic card browsing keeps its legacy order.
        else if (requestedGames.Contains("star wars unlimited"))
        {
            query = query
                .OrderBy(p => p.CatalogSet == null || p.CatalogSet.ReleasedAt == null)
                .ThenBy(p => p.CatalogSet == null ? null : p.CatalogSet.ReleasedAt)
                .ThenBy(p => p.CatalogSet == null ? string.Empty : p.CatalogSet.Code)
                .ThenBy(p => p.Number)
                .ThenBy(p => p.Card.Name)
                .ThenBy(p => p.Id);
        }
        else
        {
            query = query
                .OrderBy(p => p.CatalogSet == null ? string.Empty : p.CatalogSet.Code)
                .ThenBy(p => p.Number)
                .ThenBy(p => p.Card.Name)
                .ThenBy(p => p.Id);
        }

        var total = await query.CountAsync(ct);
        Response.Headers["X-Total-Count"] = total.ToString();

        // Apply pagination with bounds checking
        var page = Math.Max(1, qp.Page);
        var size = Math.Clamp(qp.PageSize, 1, 500);
        query = query.Skip((page - 1) * size).Take(size);

        // Project to DTO to reduce payload size and avoid over-fetching
        // Includes fallback to placeholder image when ImageUrl is null or empty
        var printings = await query
            .Include(p => p.CatalogSet)
            .Include(p => p.RarityLookup)
            .Include(p => p.Card).ThenInclude(card => card.Game)
            .Include(p => p.Card).ThenInclude(card => card.CardTypes).ThenInclude(type => type.CardType)
            .Include(p => p.Card).ThenInclude(card => card.CardAspects).ThenInclude(aspect => aspect.Aspect)
            .AsSplitQuery()
            .ToListAsync(ct);
        var rows = printings.Select(ToPrintingDto).ToList();

        return Ok(rows);
    }

    private static PrintingDto ToPrintingDto(CardPrinting printing)
        => new(
            printing.Id,
            printing.CatalogSet == null ? string.Empty : printing.CatalogSet.Name ?? printing.CatalogSet.Code,
            printing.CatalogSet?.Code,
            printing.Number,
            printing.RarityLookup?.Name ?? string.Empty,
            string.IsNullOrWhiteSpace(printing.ImageUrl)
                ? "/images/placeholders/card-3x4.png"
                : printing.ImageUrl,
            printing.CardId,
            printing.Card.Name,
            printing.Card.Game?.Name ?? string.Empty)
        {
            Style = printing.Treatment?.Name ?? "Standard",
            ReleaseDate = printing.CatalogSet?.ReleasedAt
                ?? (printing.ApiPublishedAt is { } publishedAt
                    ? DateOnly.FromDateTime(publishedAt.UtcDateTime)
                    : null),
            BackImageUrl = printing.BackImageUrl,
            ImageOrientation = printing.ArtFrontHorizontal == true ? "horizontal" :
                printing.ArtFrontHorizontal == false ? "vertical" : null,
            BackImageOrientation = printing.ArtBackHorizontal == true ? "horizontal" :
                printing.ArtBackHorizontal == false ? "vertical" : null,
            CardTypes = printing.Card.CardTypes.Select(type => type.CardType.Name)
                .Distinct().OrderBy(name => name).ToList(),
            Aspects = printing.Card.CardAspects.OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList()
        };
}
