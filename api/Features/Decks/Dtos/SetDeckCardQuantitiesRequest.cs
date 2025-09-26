namespace api.Features.Decks.Dtos;

public sealed record SetDeckCardQuantitiesRequest(
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy);
