namespace api.Features.Cards.Dtos;

/// <summary>
/// Query parameters DTO for filtering and searching card printing records.
/// Used with GET /api/cards/printings to support flexible filtering and pagination.
/// </summary>
/// <remarks>
/// This DTO provides a comprehensive set of filters for querying card printings:
/// - Game/Set/Rarity/Style: Exact match filters (case-insensitive)
/// - Number: Exact match filter for collector numbers
/// - Q: Free-text search across card name, printing number, and set name (case-insensitive LIKE)
/// - Page/PageSize: Standard pagination parameters
/// 
/// All filters are optional and can be combined. When multiple filters are specified,
/// they are applied with AND logic (all conditions must match).
/// 
/// Performance considerations:
/// - Uses SQLite NOCASE collation for case-insensitive filtering with index support
/// - PageSize is clamped to a maximum to prevent excessive load (typically 100-500)
/// - Results are sorted by Set, Number, then Card Name for consistent ordering
/// 
/// This query model is bound from query string parameters using [FromQuery] attribute.
/// </remarks>
public sealed class ListPrintingsQuery
{
    /// <summary>
    /// Optional filter by game name (e.g., "Magic", "Lorcana", "Star Wars Unlimited").
    /// Case-insensitive exact match. Null to include all games.
    /// </summary>
    public string? Game { get; init; }

    /// <summary>
    /// Optional filter by card set name (e.g., "The First Chapter", "Ursula's Return").
    /// Case-insensitive exact match. Null to include all sets.
    /// </summary>
    public string? Set { get; init; }

    /// <summary>
    /// Optional filter by collector number (e.g., "001", "147/204", "TFC-001").
    /// Case-insensitive exact match. Null to include all numbers.
    /// </summary>
    public string? Number { get; init; }

    /// <summary>
    /// Optional filter by rarity (e.g., "Common", "Rare", "Super Rare", "Legendary").
    /// Case-insensitive exact match. Null to include all rarities.
    /// </summary>
    public string? Rarity { get; init; }

    /// <summary>
    /// Optional filter by print style (e.g., "Standard", "Foil", "Hyperspace", "Showcase").
    /// Case-insensitive exact match. Null to include all styles.
    /// </summary>
    public string? Style { get; init; }

    /// <summary>
    /// Optional free-text search term for finding printings by card name, collector number, or set name.
    /// Performs case-insensitive LIKE match (substring search). Null to disable text search.
    /// Example: "elsa" would match "Elsa - Snow Queen" in any set.
    /// </summary>
    public string? Q { get; init; }

    /// <summary>
    /// Page number for pagination, 1-indexed. Defaults to 1 (first page).
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of results per page. Defaults to 100.
    /// Server may enforce a maximum limit (e.g., 500) to prevent excessive load.
    /// </summary>
    public int PageSize { get; init; } = 100;
}
