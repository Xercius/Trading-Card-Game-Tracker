namespace api.Features.Collections.Dtos;

public sealed record CollectionBulkUpdateRequest
{
    public IReadOnlyList<CollectionBulkUpdateItem> Items { get; init; } = Array.Empty<CollectionBulkUpdateItem>();
}

public sealed record CollectionBulkUpdateItem(int PrintingId, int OwnedDelta, int ProxyDelta);
