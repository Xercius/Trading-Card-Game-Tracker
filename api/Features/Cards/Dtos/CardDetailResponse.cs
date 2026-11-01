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
/// <param name="Text">Optional rules text or description of the card's abilities and effects.</param>
/// <param name="Printings">Read-only collection of all printing variations for this card, including different sets, rarities, and styles.</param>
/// <param name="CardTypes">Normalized type associations, including both primary and secondary types.</param>
/// <param name="Aspects">Normalized aspect associations with their occurrence counts.</param>
/// <param name="CardType2">Optional secondary card type (e.g., "Unit" on a Leader Unit).</param>
/// <param name="Arena">Optional arena classification (e.g., "Ground", "Space").</param>
/// <param name="Traits">Normalized trait names associated with the card.</param>
/// <param name="TextStyled">HTML-styled rules text when provided by the source catalog.</param>
/// <param name="EpicAction">Plain-text epic action ability when present.</param>
/// <param name="EpicActionStyled">HTML-styled epic action ability when present.</param>
/// <param name="DeployBox">Plain-text deploy box ability when present.</param>
/// <param name="DeployBoxStyled">HTML-styled deploy box ability when present.</param>
/// <param name="Cost">Optional resource cost of the card.</param>
/// <param name="Hp">Optional hit points / health of the card.</param>
/// <param name="Power">Optional power of the card.</param>
/// <param name="UpgradeHp">Optional hit points granted when this card is attached as an upgrade.</param>
/// <param name="UpgradePower">Optional power granted when this card is attached as an upgrade.</param>
/// <param name="Subtitle">Optional card subtitle.</param>
/// <param name="Rules">Optional plain-text rules.</param>
/// <param name="RulesStyled">Optional HTML-styled rules when provided by the source catalog.</param>
/// <param name="Side">Optional card side/alignment (e.g. "Dark", "Light").</param>
/// <param name="Uniqueness">Optional card uniqueness indicator.</param>
/// <param name="Destiny">Optional card destiny value.</param>
/// <param name="Deploy">Optional card deploy cost string.</param>
/// <param name="Forfeit">Optional card forfeit value.</param>
/// <param name="Ability">Optional card force ability level.</param>
/// <param name="Armor">Optional starship/vehicle armor value.</param>
/// <param name="Maneuver">Optional starship/vehicle maneuver value.</param>
/// <param name="Hyperspeed">Optional starship hyperspeed value.</param>
/// <param name="Landspeed">Optional vehicle landspeed value.</param>
/// <param name="Parsec">Optional system parsec number.</param>
/// <param name="Ferocity">Optional creature ferocity value.</param>
/// <param name="Politics">Optional character politics value.</param>
/// <param name="Lore">Optional card flavor/lore text.</param>
/// <param name="ExtraCardDataJson">Optional serialized JSON containing SWCCG extra card data (pulledBy, combos, matching, etc.).</param>
/// <param name="BackTitle">Optional title for card back (Objectives).</param>
/// <param name="BackText">Optional gametext for card back (Objectives).</param>
/// <param name="Persona">Optional character persona(s).</param>
/// <param name="SubTypes">Optional normalized subtype associations.</param>
/// <param name="LightSideIcons">Optional number of Light Side Force generation icons.</param>
/// <param name="DarkSideIcons">Optional number of Dark Side Force generation icons.</param>
/// <param name="BackLightSideIcons">Optional number of Light Side Force generation icons on card back.</param>
/// <param name="BackDarkSideIcons">Optional number of Dark Side Force generation icons on card back.</param>
public sealed record CardDetailResponse(
    int Id,
    string Name,
    string Game,
    string CardType,
    string? Text,
    IReadOnlyList<CardPrintingResponse> Printings,
    IReadOnlyList<string>? CardTypes = null,
    IReadOnlyList<CardAspectResponse>? Aspects = null,
    string? CardType2 = null,
    string? Arena = null,
    IReadOnlyList<string>? Traits = null,
    string? TextStyled = null,
    string? EpicAction = null,
    string? EpicActionStyled = null,
    string? DeployBox = null,
    string? DeployBoxStyled = null,
    int? Cost = null,
    int? Hp = null,
    int? Power = null,
    int? UpgradeHp = null,
    int? UpgradePower = null,
    string? Subtitle = null,
    string? Rules = null,
    string? RulesStyled = null,
    string? Side = null,
    string? Uniqueness = null,
    string? Destiny = null,
    string? Deploy = null,
    string? Forfeit = null,
    int? Ability = null,
    string? Armor = null,
    int? Maneuver = null,
    int? Hyperspeed = null,
    int? Landspeed = null,
    int? Parsec = null,
    string? Ferocity = null,
    int? Politics = null,
    string? Lore = null,
    string? ExtraCardDataJson = null,
    string? BackTitle = null,
    string? BackText = null,
    string? Persona = null,
    IReadOnlyList<string>? SubTypes = null,
    int? LightSideIcons = null,
    int? DarkSideIcons = null,
    int? BackLightSideIcons = null,
    int? BackDarkSideIcons = null);

public sealed record CardAspectResponse(string Name, int Count);
