using System.Text.Json.Serialization;

namespace api.Features.Cards.Dtos;

/// <summary>
/// Represents a single facet option with its value and count.
/// Used to show available filter options with the number of matching cards.
/// </summary>
public sealed record FacetOption
{
    /// <summary>
    /// The facet value (e.g., "Magic", "Alpha", "Common").
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Number of cards that match this facet value with current filters applied.
    /// </summary>
    public required int Count { get; init; }
}

/// <summary>
/// Response DTO containing all facet options with counts for games, sets, and rarities.
/// Each facet list reflects the current filter state and shows only values that produce results.
/// </summary>
/// <remarks>
/// This DTO is used by the unified facets endpoint that provides context-sensitive filter options.
/// All facet lists are computed based on the current filter criteria (game, set, rarity, search term).
/// Each option includes a count showing how many cards match that specific facet value.
/// 
/// Returned by: GET /api/cards/facets?game={game}&amp;set={set}&amp;rarity={rarity}&amp;q={q}
/// </remarks>
public sealed record CardsFacetResponse
{
    /// <summary>
    /// List of available game facets with counts based on current filters.
    /// Empty array if no games match the current filter criteria.
    /// </summary>
    public IReadOnlyList<FacetOption> Games { get; init; } = Array.Empty<FacetOption>();

    /// <summary>
    /// List of available set facets with counts based on current filters.
    /// Empty array if no sets match the current filter criteria.
    /// </summary>
    public IReadOnlyList<FacetOption> Sets { get; init; } = Array.Empty<FacetOption>();

    /// <summary>
    /// List of available rarity facets with counts based on current filters.
    /// Empty array if no rarities match the current filter criteria.
    /// </summary>
    public IReadOnlyList<FacetOption> Rarities { get; init; } = Array.Empty<FacetOption>();
}

/// <summary>
/// Response DTO containing distinct card set names, optionally filtered by game.
/// Used for providing filter facets/options in UI dropdowns and search interfaces.
/// </summary>
/// <remarks>
/// This DTO supports multi-game filtering scenarios:
/// - When Game is null: Returns sets across all games
/// - When Game is set: Returns sets only for the specified game
/// 
/// The Game property is conditionally serialized (omitted when null) to keep responses clean.
/// Sets are returned in alphabetical order and exclude empty/whitespace values.
/// 
/// Returned by: GET /api/cards/facets/sets?game={game}
/// </remarks>
public sealed record CardFacetSetsResponse
{
    /// <summary>
    /// The game filter applied to this response. Null if sets span multiple games.
    /// Omitted from JSON serialization when null to reduce payload size.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; init; }

    /// <summary>
    /// Distinct list of set names available for the filtered scope.
    /// Empty array if no sets found. Sorted alphabetically.
    /// </summary>
    public IReadOnlyList<string> Sets { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Response DTO containing distinct card rarity values, optionally filtered by game.
/// Used for providing filter facets/options in UI dropdowns and search interfaces.
/// </summary>
/// <remarks>
/// This DTO supports multi-game filtering scenarios:
/// - When Game is null: Returns rarities across all games
/// - When Game is set: Returns rarities only for the specified game
/// 
/// The Game property is conditionally serialized (omitted when null) to keep responses clean.
/// Rarities are returned in alphabetical order and exclude empty/whitespace values.
/// Common rarity values include: "Common", "Uncommon", "Rare", "Super Rare", "Legendary", etc.
/// 
/// Returned by: GET /api/cards/facets/rarities?game={game}
/// </remarks>
public sealed record CardFacetRaritiesResponse
{
    /// <summary>
    /// The game filter applied to this response. Null if rarities span multiple games.
    /// Omitted from JSON serialization when null to reduce payload size.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; init; }

    /// <summary>
    /// Distinct list of rarity values available for the filtered scope.
    /// Empty array if no rarities found. Sorted alphabetically.
    /// </summary>
    public IReadOnlyList<string> Rarities { get; init; } = Array.Empty<string>();
}
