namespace api.Features.Values.Dtos;

public sealed record SeriesResponse(int CardPrintingId, IEnumerable<SeriesPointResponse> Points);
