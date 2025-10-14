namespace api.Features.Cards.Dtos;

/// <summary>
/// DTO for card printing records that includes both printing details and parent card context.
/// Used in printing list endpoints where card information needs to be denormalized for efficient display.
/// </summary>
/// <remarks>
/// This DTO differs from CardPrintingResponse in that it includes full card context (CardId, CardName, Game)
/// alongside the printing details. This denormalization is intentional to avoid N+1 query issues and
/// provide all necessary information in a single response for printing list views.
/// 
/// Use cases:
/// - GET /api/cards/printings - List all printings with card context
/// - Printing search/filter results where you need both printing and card information
/// - Export/reporting scenarios requiring complete printing data
/// 
/// The DTO includes:
/// - Printing-specific: PrintingId, SetName, SetCode, Number, Rarity, ImageUrl
/// - Card context: CardId, CardName, Game
/// 
/// This is typically projected directly from EF Core queries using Include(cp => cp.Card)
/// to efficiently fetch both entities in a single database round-trip.
/// </remarks>
/// <param name="PrintingId">Unique identifier for this specific printing in the database.</param>
/// <param name="SetName">Name of the card set/expansion this printing belongs to (e.g., "The First Chapter", "Ursula's Return").</param>
/// <param name="SetCode">Optional short code for the set (e.g., "TFC", "URR"). Null if not available.</param>
/// <param name="Number">Collector or card number within the set (e.g., "001", "147/204", "TFC-001").</param>
/// <param name="Rarity">Rarity classification for this printing (e.g., "Common", "Rare", "Super Rare", "Legendary").</param>
/// <param name="ImageUrl">Optional URL to the card's image for this specific printing. Null if no image is available.</param>
/// <param name="CardId">Unique identifier for the parent card that this printing represents.</param>
/// <param name="CardName">Display name of the parent card (e.g., "Lightning Bolt", "Elsa - Snow Queen").</param>
/// <param name="Game">Name of the trading card game this card belongs to (e.g., "Magic", "Lorcana", "Star Wars Unlimited").</param>
public sealed record PrintingDto(
    int PrintingId,
    string SetName,
    string? SetCode,
    string Number,
    string Rarity,
    string? ImageUrl,
    int CardId,
    string CardName,
    string Game
);
