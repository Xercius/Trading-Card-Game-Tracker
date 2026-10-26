namespace api.Models;

public sealed class CardAspect
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int AspectId { get; set; }
    public Aspect Aspect { get; set; } = null!;

    public int Count { get; set; } = 1;
}
