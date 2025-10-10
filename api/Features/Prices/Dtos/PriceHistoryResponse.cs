using System.Text.Json.Serialization;

namespace api.Features.Prices.Dtos;

public sealed record PriceHistoryResponse([
    property: JsonPropertyName("points")
] IReadOnlyList<PricePointDto> Points);

public sealed record PricePointDto(
    [property: JsonPropertyName("d")] string Date,
    [property: JsonPropertyName("p")] decimal Price
);
