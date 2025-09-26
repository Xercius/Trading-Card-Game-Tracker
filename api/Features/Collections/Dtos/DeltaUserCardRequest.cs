namespace api.Features.Collections.Dtos;

public sealed record DeltaUserCardRequest(
    int CardPrintingId,
    int DeltaOwned,
    int DeltaWanted,
    int DeltaProxyOwned);
