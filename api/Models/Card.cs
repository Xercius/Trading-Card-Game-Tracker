using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Card
{
    [Key]
    public int Id { get; set; }
    public required string Game { get; set; } // Magic, Lorcana, Star Wars Unlimited etc..
    public required string Name { get; set; } // Name of card
    public required string CardType { get; set; } // Unit, Instant, Sorcery, Upgrade, Enchantment etc..
    public string? Description { get; set; } // Optional rules text
    public string? DetailsJson { get; set; } // Source-specific payload

    /// <summary>
    /// For variant cards (alternate art, foil showcase, etc.) this points to the base/canonical
    /// card that this card is a variant of. Populated during import from the API's variantOf field.
    /// </summary>
    public int? BaseCardId { get; set; }
    public Card? BaseCard { get; set; }

    public ICollection<CardPrinting> Printings { get; set; } = new List<CardPrinting>();
    public ICollection<Card> Variants { get; set; } = new List<Card>();
}
