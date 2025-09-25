namespace api.Features.Cards.Dtos;

public sealed record UpsertPrintingRequest(
    int? Id,
    int CardId,
    string? Set,
    string? Number,
    string? Rarity,
    string? Style,
    string? ImageUrl,
    bool ImageUrlSet = false);
