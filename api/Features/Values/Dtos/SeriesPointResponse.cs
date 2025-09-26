namespace api.Features.Values.Dtos;

public sealed record SeriesPointResponse(DateTime AsOfUtc, long PriceCents, string Source);
