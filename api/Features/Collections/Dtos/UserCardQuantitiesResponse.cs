namespace api.Features.Collections.Dtos;

public sealed record UserCardQuantitiesResponse(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityProxyOwned,
    int QuantityWanted);

