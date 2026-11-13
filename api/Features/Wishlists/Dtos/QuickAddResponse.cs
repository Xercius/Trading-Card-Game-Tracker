using System.Text.Json.Serialization;

namespace api.Features.Wishlists.Dtos;

/// <summary>
/// Response payload returned after quick-adding a printing to a wishlist.
/// </summary>
public sealed record QuickAddResponse(
    [property: JsonPropertyName("printingId")] int PrintingId,
    [property: JsonPropertyName("quantityWanted")] int QuantityWanted);
