namespace api.Features.Decks.Dtos;

public sealed record UpsertDeckCardFullRequest(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy);
