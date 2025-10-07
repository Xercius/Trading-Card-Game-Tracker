using System.Text.Json.Serialization;

namespace api.Features.Prices.Dtos;

public sealed record DailyValuePointDto(
    [property: JsonPropertyName("d")] string Date,
    [property: JsonPropertyName("v")] decimal? Value);
