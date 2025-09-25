namespace api.Features.Values.Dtos;

public sealed record CollectionSummaryResponse(long TotalCents, IEnumerable<GameSliceResponse> ByGame);
