namespace api.Features.Wishlists.Dtos;

public sealed record UpsertWishlistRequest(
    int CardPrintingId,
    int QuantityWanted,
    int? DeckId = null);
