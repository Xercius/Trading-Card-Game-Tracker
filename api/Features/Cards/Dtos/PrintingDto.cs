namespace api.Features.Cards.Dtos;

public sealed record PrintingDto(
    int PrintingId,
    string SetName,
    string? SetCode,
    string Number,
    string Rarity,
    string ImageUrl
);
