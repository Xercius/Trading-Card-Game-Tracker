using System.Text.Json.Serialization;

namespace api.Features.Collections.Dtos;

public sealed record QuickAddRequest(
    [property: JsonPropertyName("printingId")] int PrintingId,
    [property: JsonPropertyName("quantity")] int Quantity
);

public sealed record QuickAddResponse(
    [property: JsonPropertyName("printingId")] int PrintingId,
    [property: JsonPropertyName("quantityOwned")] int QuantityOwned
);
