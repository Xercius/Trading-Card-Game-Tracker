namespace api.Features.Cards.Dtos;

/// <summary>
/// Response DTO representing a specific printing variation of a card.
/// Contains the essential printing details without including parent card context.
/// Used as a nested object within CardDetailResponse to represent each printing of a card.
/// </summary>
/// <remarks>
/// A "printing" represents a specific physical version of a card, distinguished by:
/// - The set/expansion it was released in
/// - Its collector number within that set
/// - Its rarity tier in that set
/// - Its print style/finish (standard, foil, etc.)
/// - Optionally, a unique image URL if artwork varies
/// 
/// Multiple printings of the same card can exist (e.g., reprints in different sets,
/// different foil treatments, promotional versions, etc.).
/// 
/// This DTO is lightweight and focused on printing-specific attributes, while the parent
/// CardDetailResponse provides the card's base information (name, type, description).
/// </remarks>
/// <param name="Id">Unique identifier for this specific printing in the database.</param>
/// <param name="Set">Name of the card set/expansion this printing belongs to (e.g., "The First Chapter", "Ursula's Return").</param>
/// <param name="Number">Collector or card number within the set (e.g., "001", "147/204", "TFC-001").</param>
/// <param name="Rarity">Rarity classification for this printing (e.g., "Common", "Rare", "Super Rare", "Legendary").</param>
/// <param name="Style">Print style or finish of this card (e.g., "Standard", "Foil", "Hyperspace", "Showcase").</param>
/// <param name="ImageUrl">Optional URL to the card's image for this specific printing. Null if no image is available.</param>
public sealed record CardPrintingResponse(
    int Id,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl);
