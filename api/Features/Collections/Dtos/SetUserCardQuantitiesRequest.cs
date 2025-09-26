namespace api.Features.Collections.Dtos;

public sealed record SetUserCardQuantitiesRequest(
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned);
