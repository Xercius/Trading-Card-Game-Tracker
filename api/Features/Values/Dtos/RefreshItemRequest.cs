namespace api.Features.Values.Dtos;

public sealed record RefreshItemRequest(int CardPrintingId, long PriceCents, string? Source);
