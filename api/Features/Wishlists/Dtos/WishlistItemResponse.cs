namespace api.Features.Wishlists.Dtos;

/// <summary>Represents a single source contributing to a card printing's wishlist demand.</summary>
public sealed record WishlistSourceDto(
    int? DeckId,
    string DeckName,
    int Quantity);

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
    string? ImageUrl,
    IReadOnlyList<WishlistSourceDto> Sources);
