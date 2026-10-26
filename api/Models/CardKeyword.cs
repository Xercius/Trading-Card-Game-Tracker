namespace api.Models;

public sealed class CardKeyword
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int KeywordId { get; set; }
    public Keyword Keyword { get; set; } = null!;
}
