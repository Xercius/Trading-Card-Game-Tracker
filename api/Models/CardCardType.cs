namespace api.Models;

public sealed class CardCardType
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int CardTypeId { get; set; }
    public CardType CardType { get; set; } = null!;

    public required string SourceRole { get; set; }
}
