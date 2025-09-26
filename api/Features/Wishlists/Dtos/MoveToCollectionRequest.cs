namespace api.Features.Wishlists.Dtos;

public sealed record MoveToCollectionRequest(
    int CardPrintingId,
    int Quantity,
    bool UseProxy = false);
