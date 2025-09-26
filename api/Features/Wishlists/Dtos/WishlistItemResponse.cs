namespace api.Features.Wishlists.Dtos;

public sealed record WishlistItemResponse(
    int CardPrintingId,
    int QuantityWanted,
    int CardId,
    string CardName,
    string Game,
    string Set,
    string Number,
    string Rarity,
    string Style,
    string? ImageUrl);
