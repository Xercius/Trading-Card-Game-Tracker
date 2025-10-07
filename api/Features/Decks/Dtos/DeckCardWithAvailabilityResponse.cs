namespace api.Features.Decks.Dtos;

public sealed record DeckCardWithAvailabilityResponse(
    int PrintingId,
    string CardName,
    string? ImageUrl,
    int QuantityInDeck,
    int Availability,
    int AvailabilityWithProxies);
