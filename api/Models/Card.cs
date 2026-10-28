using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Card
{
    [Key]
    public int Id { get; set; }
    public required string Game { get; set; } // Magic, Lorcana, Star Wars Unlimited etc..
    public required string Name { get; set; } // Name of card
    public required string CardType { get; set; } // Unit, Instant, Sorcery, Upgrade, Enchantment etc..
    public string? Text { get; set; } // Optional rules text
    public string? TextStyled { get; set; }
    public string? DeployBox { get; set; }
    public string? DeployBoxStyled { get; set; }
    public string? EpicAction { get; set; }
    public string? EpicActionStyled { get; set; }
    public string? Rules { get; set; }
    public string? RulesStyled { get; set; }
    public string? DetailsJson { get; set; } // Source-specific payload
    public int? StrapiId { get; set; }
    public string? CardUid { get; set; }
    public string? Subtitle { get; set; }
    public string? CardType2 { get; set; }
    public string? Arena { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Hp { get; set; }
    public int? UpgradeHp { get; set; }
    public int? UpgradePower { get; set; }
    public string? Aspects { get; set; }
    public string? Traits { get; set; }
    public string? Keywords { get; set; }
    public int? BaseStrapiId { get; set; }
    public DateTimeOffset? ApiCreatedAt { get; set; }
    public DateTimeOffset? ApiUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// For variant cards (alternate art, foil showcase, etc.) this points to the base/canonical
    /// card that this card is a variant of. Populated during import from the API's variantOf field.
    /// </summary>
    public int? BaseCardId { get; set; }
    public Card? BaseCard { get; set; }

    public ICollection<CardPrinting> Printings { get; set; } = new List<CardPrinting>();
    public ICollection<Card> Variants { get; set; } = new List<Card>();
    public ICollection<CardCardType> CardTypes { get; set; } = new List<CardCardType>();
    public ICollection<CardTrait> CardTraits { get; set; } = new List<CardTrait>();
    public ICollection<CardKeyword> CardKeywords { get; set; } = new List<CardKeyword>();
    public ICollection<CardArena> CardArenas { get; set; } = new List<CardArena>();
    public ICollection<CardAspect> CardAspects { get; set; } = new List<CardAspect>();
}
