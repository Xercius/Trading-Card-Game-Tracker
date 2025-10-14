namespace api.Features.Cards.Dtos;

/// <summary>
/// Response DTO for a single card item in a paginated list view.
/// Provides card summary information with its primary/representative printing for efficient list rendering.
/// </summary>
/// <remarks>
/// This DTO is optimized for list views where showing all printings would be too verbose.
/// Instead, it includes a single "primary" printing (typically the most relevant or first printing)
/// along with a count of total printings available for the card.
/// 
/// The Primary property may be null if the card has no associated printings.
/// PrintingsCount indicates the total number of printing variations available.
/// 
/// Typically returned by:
/// - GET /api/cards (list view) - Paginated card browsing
/// - Card search/filter endpoints
/// </remarks>
public sealed class CardListItemResponse
{
    /// <summary>
    /// Unique identifier for the card in the database.
    /// </summary>
    public int CardId { get; set; }

    /// <summary>
    /// Name of the trading card game this card belongs to (e.g., "Magic", "Lorcana", "Star Wars Unlimited").
    /// </summary>
    public required string Game { get; set; }

    /// <summary>
    /// Display name of the card (e.g., "Lightning Bolt", "Elsa - Snow Queen").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type classification of the card (e.g., "Unit", "Instant", "Sorcery", "Upgrade", "Enchantment").
    /// </summary>
    public required string CardType { get; set; }

    /// <summary>
    /// The primary or representative printing for this card to display in list views.
    /// Null if the card has no printings. Typically the first or most common printing.
    /// </summary>
    public PrimaryPrintingResponse? Primary { get; set; }

    /// <summary>
    /// Total number of different printings/variations available for this card.
    /// Includes all combinations of sets, rarities, and styles.
    /// </summary>
    public int PrintingsCount { get; set; }

    /// <summary>
    /// Nested response DTO representing a single printing variation of a card.
    /// Contains the essential printing details needed for list view display.
    /// </summary>
    public sealed class PrimaryPrintingResponse
    {
        /// <summary>
        /// Unique identifier for this specific printing in the database.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the card set this printing belongs to (e.g., "The First Chapter", "Ursula's Return").
        /// </summary>
        public required string Set { get; set; }

        /// <summary>
        /// Collector or card number within the set (e.g., "001", "147/204", "TFC-001").
        /// </summary>
        public required string Number { get; set; }

        /// <summary>
        /// Rarity classification for this printing (e.g., "Common", "Rare", "Super Rare", "Legendary").
        /// </summary>
        public required string Rarity { get; set; }

        /// <summary>
        /// Print style or finish of this card (e.g., "Standard", "Foil", "Hyperspace", "Showcase").
        /// </summary>
        public required string Style { get; set; }

        /// <summary>
        /// Optional URL to the card's image. Null if no image is available.
        /// </summary>
        public string? ImageUrl { get; set; }
    }
}
