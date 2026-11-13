namespace api.Features.Values.Dtos;

public sealed record SeriesPointResponse(DateTimeOffset AsOfUtc, long PriceCents, string Source);
