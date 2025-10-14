namespace api.Features.Cards.Dtos;

/// <summary>
/// Response DTO for detailed card information including all associated printings.
/// Used when retrieving a single card's complete details or when the includePrintings flag is set.
/// Maps from the Card entity and includes all CardPrinting records as nested CardPrintingResponse objects.
/// </summary>
/// <remarks>
/// This DTO represents the complete view of a trading card across all supported games (Magic, Lorcana, Star Wars Unlimited, etc.).
/// It aggregates the base card information (name, type, description) with all printing variations
/// that have been released for this card (different sets, rarities, styles, etc.).
/// 
/// Typically returned by:
/// - GET /api/cards/{id} - Single card detail endpoint
/// - GET /api/cards (when includePrintings=true) - List cards with full printing information
/// </remarks>
/// <param name="Id">Unique identifier for the card in the database.</param>
/// <param name="Name">Display name of the card (e.g., "Lightning Bolt", "Elsa - Snow Queen").</param>
/// <param name="Game">Name of the trading card game this card belongs to (e.g., "Magic", "Lorcana", "Star Wars Unlimited").</param>
/// <param name="CardType">Type classification of the card (e.g., "Unit", "Instant", "Sorcery", "Upgrade", "Enchantment").</param>
/// <param name="Description">Optional rules text or description of the card's abilities and effects.</param>
/// <param name="Printings">Read-only collection of all printing variations for this card, including different sets, rarities, and styles.</param>
public sealed record CardDetailResponse(
    int Id,
    string Name,
    string Game,
    string CardType,
    string? Description,
    IReadOnlyList<CardPrintingResponse> Printings);
