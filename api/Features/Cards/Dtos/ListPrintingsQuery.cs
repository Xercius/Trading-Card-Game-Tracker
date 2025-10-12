namespace api.Features.Cards.Dtos;

public sealed class ListPrintingsQuery
{
    public string? Game { get; init; }
    public string? Set { get; init; }
    public string? Rarity { get; init; }
    public string? Style { get; init; }
    public string? Q { get; init; }           // name/number search
    public int Page { get; init; } = 1;       // optional paging
    public int PageSize { get; init; } = 100;
}
