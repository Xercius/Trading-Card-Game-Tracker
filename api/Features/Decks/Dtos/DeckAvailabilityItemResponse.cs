namespace api.Features.Decks.Dtos;

public sealed record DeckAvailabilityItemResponse(
    int CardPrintingId,
    int Owned,
    int Proxy,
    int Assigned,
    int Available,
    int AvailableWithProxy);
