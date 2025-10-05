namespace api.Features.Cards.Dtos;

public sealed class CardListPageResponse
{
    public required IReadOnlyList<CardListItemResponse> Items { get; set; }
    public int? Total { get; set; }
    public int? NextSkip { get; set; }
}
