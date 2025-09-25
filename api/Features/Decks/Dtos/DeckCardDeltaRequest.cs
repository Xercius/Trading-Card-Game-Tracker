namespace api.Features.Decks.Dtos;

public sealed record DeckCardDeltaRequest(
    int CardPrintingId,
    int DeltaInDeck,
    int DeltaIdea,
    int DeltaAcquire,
    int DeltaProxy);
