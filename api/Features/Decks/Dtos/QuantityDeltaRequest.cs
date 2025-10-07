namespace api.Features.Decks.Dtos;

public sealed record QuantityDeltaRequest
{
    public int PrintingId { get; init; }
    public int QtyDelta { get; init; }
}
