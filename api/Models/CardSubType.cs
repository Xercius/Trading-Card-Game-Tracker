namespace api.Models;

public sealed class CardSubType
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int SubTypeId { get; set; }
    public SubType SubType { get; set; } = null!;
}

