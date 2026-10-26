namespace api.Models;

public sealed class CardArena
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int ArenaId { get; set; }
    public Arena Arena { get; set; } = null!;
}
