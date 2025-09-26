namespace api.Features.Decks.Dtos;

public sealed record DeckCardItemResponse(
    int CardPrintingId,
    int QuantityInDeck,
    int QuantityIdea,
    int QuantityAcquire,
    int QuantityProxy,
    int CardId,
    string CardName,
    string Game,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl);
