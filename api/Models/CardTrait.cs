namespace api.Models;

public sealed class CardTrait
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int TraitId { get; set; }
    public Trait Trait { get; set; } = null!;
}
