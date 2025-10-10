using System.Text.Json.Serialization;

namespace api.Features.Cards.Dtos;

public sealed record CardFacetSetsResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; init; }

    public IReadOnlyList<string> Sets { get; init; } = Array.Empty<string>();
}

public sealed record CardFacetRaritiesResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; init; }

    public IReadOnlyList<string> Rarities { get; init; } = Array.Empty<string>();
}
