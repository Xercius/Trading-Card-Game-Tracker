namespace api.Features.Cards.Dtos;

public sealed record CardPrintingResponse(
    int Id,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl);
