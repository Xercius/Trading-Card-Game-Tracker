namespace api.Features.Decks.Dtos;

public sealed record UpsertDeckCardRequest(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy);
