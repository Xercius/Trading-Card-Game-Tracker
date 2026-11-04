namespace api.Features.Collections.Dtos;

public sealed record CollectionBatchSetItem(
    int CardPrintingId,
    int QuantityOwned,
    int QuantityProxyOwned,
    int QuantityWanted);

public sealed record CollectionBatchSetRequest(
    IReadOnlyList<CollectionBatchSetItem>? Items);

