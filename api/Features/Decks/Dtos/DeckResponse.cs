namespace api.Features.Decks.Dtos;

/// <summary>
/// Featured card in a deck (e.g. Leader for SWU or Objective for SWCCG).
/// </summary>
public sealed record DeckFeaturedCardDto(
    int CardPrintingId,
    string CardName,
    string? ImageUrl,
    string CardType,
    bool IsHorizontal
);

/// <summary>
/// Represents a user's deck with summary details and featured cards.
/// </summary>
public sealed record DeckResponse(
    int Id,
    int UserId,
    string Game,
    string Name,
    string? Description,
    DateTime CreatedUtc,
    DateTime? UpdatedUtc,
    int CardCount = 0,
    IReadOnlyList<DeckFeaturedCardDto>? FeaturedCards = null,
    string? Format = null,
    string? Side = null,
    IReadOnlyList<string>? Aspects = null,
    DeckFeaturedCardDto? BaseCard = null,
    int DefensiveShieldCount = 0);
