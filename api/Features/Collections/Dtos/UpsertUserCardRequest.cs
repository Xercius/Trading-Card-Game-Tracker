namespace api.Features.Collections.Dtos;

public sealed record UpsertUserCardRequest(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityWanted,
    int QuantityProxyOwned);
