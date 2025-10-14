using System.Text.Json.Serialization;

namespace api.Features.Cards.Dtos;

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
