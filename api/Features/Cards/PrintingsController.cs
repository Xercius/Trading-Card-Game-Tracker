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
    /// Results are sorted by Set, Number, then Card Name and paginated.
    /// </returns>
    /// <remarks>
    /// Performance considerations:
    /// - Uses EF.Functions.Collate with NOCASE for case-insensitive filtering that leverages SQLite collation indices
    /// - Projects to PrintingDto to minimize payload size and prevent N+1 queries by including Card navigation
    /// - Pagination defaults to page 1, page size 100 (clamped to max 500)
    /// - Search term (Q) performs LIKE match against card name, printing number, and set name
    /// </remarks>
    [HttpGet]
    // Suggest: rename 'qp' → 'queryParams'
    public async Task<ActionResult<IEnumerable<PrintingDto>>> Get([FromQuery] ListPrintingsQuery qp, CancellationToken ct)
    {
        // Build base query with eager loading of Card for name and game information
        var query = _db.Set<CardPrinting>()
            .AsNoTracking()
            .Include(p => p.Card) // need Name + Game
            .AsQueryable();

        // Filter by game(s) - supports comma-separated list via CsvUtils
        var games = CsvUtils.Parse(qp.Game); // Suggest: rename 'games' → 'gameNames'
        if (games.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                normalizedGames.Contains(EF.Property<string>(p.Card, "GameNorm")));
        }

        // Filter by set(s) - supports comma-separated list
        var sets = CsvUtils.Parse(qp.Set); // Suggest: rename 'sets' → 'setNames'
        if (sets.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedSets = sets.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                normalizedSets.Contains(EF.Property<string>(p, "SetNorm")));
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
                normalizedRarities.Contains(EF.Property<string>(p, "RarityNorm")));
        }

        // Filter by style(s) - supports comma-separated list
        var styles = CsvUtils.Parse(qp.Style);
        if (styles.Count > 0)
        {
            // Case-insensitive match using normalized column for index efficiency
            var normalizedStyles = styles.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(p =>
                normalizedStyles.Contains(EF.Property<string>(p, "StyleNorm")));
        }

        // Free-text search across card name, printing number, and set name
        if (!string.IsNullOrWhiteSpace(qp.Q))
        {
            var term = qp.Q.Trim(); // Suggest: rename 'term' → 'searchTerm'
            var normalizedTerm = SqliteCaseNormalizer.Normalize(term);
            var normalizedPattern = $"%{normalizedTerm}%";
            var pattern = $"%{term}%";
            // Use normalized columns where available for index-friendly case-insensitive wildcard search
            query = query.Where(p =>
                (p.Card.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Card.Name, "NOCASE"), pattern)) ||
                (p.Number != null && EF.Functions.Like(EF.Functions.Collate(p.Number, "NOCASE"), pattern)) ||
                (p.Set != null && EF.Functions.Like(EF.Property<string>(p, "SetNorm"), normalizedPattern)));
        }

        // Apply default ordering: set, then number, then card name
        query = query
            .OrderBy(p => p.Set)
            .ThenBy(p => p.Number)
            .ThenBy(p => p.Card.Name);

        // Apply pagination with bounds checking
        var page = Math.Max(1, qp.Page);
        var size = Math.Clamp(qp.PageSize, 1, 500);
        query = query.Skip((page - 1) * size).Take(size);

        // Project to DTO to reduce payload size and avoid over-fetching
        // Includes fallback to placeholder image when ImageUrl is null or empty
        var rows = await query // Suggest: rename 'rows' → 'printings'
            .Select(p => new PrintingDto(
                p.Id,
                p.Set,
                null,
                p.Number,
                p.Rarity,
                (p.ImageUrl == null || p.ImageUrl.Trim() == "")
                    ? "/images/placeholders/card-3x4.png"
                    : p.ImageUrl,
                p.CardId,
                p.Card.Name,
                p.Card.Game
            ))
            .ToListAsync(ct);

        return Ok(rows);
    }
}