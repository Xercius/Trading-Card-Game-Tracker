namespace api.Features.Cards.Dtos;

public sealed record CardDetailResponse(
    int Id,
    string Name,
    string Game,
    string CardType,
    string? Description,
    IReadOnlyList<CardPrintingResponse> Printings);
