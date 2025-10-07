using System;
using System.Collections.Generic;

namespace api.Features.Collections.Dtos;

public sealed class CollectionBulkUpdateRequest
{
    public IReadOnlyList<CollectionBulkUpdateItem> Items { get; init; } = Array.Empty<CollectionBulkUpdateItem>();
}

public sealed record CollectionBulkUpdateItem(int PrintingId, int OwnedDelta, int ProxyDelta);
