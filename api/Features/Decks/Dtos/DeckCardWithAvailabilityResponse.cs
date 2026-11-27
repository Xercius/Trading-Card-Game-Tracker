using api.Features.Cards.Dtos;

namespace api.Features.Decks.Dtos;

public sealed record DeckCardWithAvailabilityResponse(
    int PrintingId,
    string CardName,
    string? ImageUrl,
    int QuantityInDeck,
    int Availability,
    int AvailabilityWithProxies,
    string? CardType = null,
    bool IsHorizontal = false,
    bool IsStarting = false,
    int CardId = 0,
    int? Cost = null,
    IReadOnlyList<CardAspectResponse>? Aspects = null,
    string? Side = null,
    string? SetName = null,
    int QuantitySideboard = 0,
    bool IsCommander = false,
    IReadOnlyList<string>? ColorIdentity = null,
    bool CanBeCommander = false,
    bool IsBasicLand = false,
    bool HasUnlimitedCopies = false,
    int? MaxAllowedCopies = null,
    int QuantityIdea = 0,
    int QuantityOwned = 0,
    int QuantityProxyOwned = 0,
    int MissingQuantity = 0);
