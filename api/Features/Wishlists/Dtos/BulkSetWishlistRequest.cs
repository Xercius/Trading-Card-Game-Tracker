namespace api.Features.Wishlists.Dtos;

public sealed record BulkSetWishlistRequest(
    int CardPrintingId,
    int QuantityWanted);
