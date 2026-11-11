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
    string? SetName = null);
