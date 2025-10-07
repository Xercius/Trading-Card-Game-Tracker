namespace api.Features.Wishlists.Dtos;

public sealed record MoveToCollectionResponse(
    int PrintingId,
    int WantedAfter,
    int OwnedAfter,
    int ProxyAfter,
    int Availability,
    int AvailabilityWithProxies);
