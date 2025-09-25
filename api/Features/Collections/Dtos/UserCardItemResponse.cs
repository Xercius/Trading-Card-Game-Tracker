namespace api.Features.Collections.Dtos;

public sealed record UserCardItemResponse(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned,
    int CardId,
    string CardName,
    string Game,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl);
