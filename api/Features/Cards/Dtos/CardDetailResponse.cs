namespace api.Features.Cards.Dtos;

public sealed record CardDetailResponse(
    int CardId,
    string Name,
    string Game,
    IReadOnlyList<CardPrintingResponse> Printings);
