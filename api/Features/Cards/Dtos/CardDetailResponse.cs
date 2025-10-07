namespace api.Features.Cards.Dtos;

public sealed record CardDetailResponse(
    int CardId,
    string Name,
    string Game,
    string CardType,
    string? Description,
    IReadOnlyList<CardPrintingResponse> Printings);
