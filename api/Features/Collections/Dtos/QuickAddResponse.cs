using System.Text.Json.Serialization;

namespace api.Features.Collections.Dtos;

/// <summary>
/// Response payload returned after quick-adding a printing to a collection.
/// </summary>
public sealed record QuickAddResponse(
    [property: JsonPropertyName("printingId")] int PrintingId,
    [property: JsonPropertyName("quantityOwned")] int QuantityOwned);
