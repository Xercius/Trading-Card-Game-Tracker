using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Card
{
    [Key]
    public int Id { get; set; }
    public int? GameId { get; set; }
    public Game? Game { get; set; }
    public required string Name { get; set; } // Name of card
    public string? Text { get; set; } // Optional rules text
    public string? TextStyled { get; set; }
    public string? DeployBox { get; set; }
    public string? DeployBoxStyled { get; set; }
    public string? EpicAction { get; set; }
    public string? EpicActionStyled { get; set; }
    public string? Rules { get; set; }
    public string? RulesStyled { get; set; }
    public string? CardUid { get; set; }
    public string? Subtitle { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Hp { get; set; }
    public int? UpgradeHp { get; set; }
    public int? UpgradePower { get; set; }
    public string? Side { get; set; }
    public string? Uniqueness { get; set; }
    public string? Destiny { get; set; }
    public string? Deploy { get; set; }
    public string? Forfeit { get; set; }
    public int? Ability { get; set; }
    public string? Armor { get; set; }
    public int? Maneuver { get; set; }
    public int? Hyperspeed { get; set; }
    public int? Landspeed { get; set; }
    public int? Parsec { get; set; }
    public string? Ferocity { get; set; }
    public int? Politics { get; set; }
    public string? Persona { get; set; }
    [MaxLength(32)]
    public string? Stage { get; set; }
    public string? Lore { get; set; }
    public string? ExtraCardDataJson { get; set; }
    public string? BackTitle { get; set; }
    public string? BackText { get; set; }
    public int? LightSideIcons { get; set; }
    public int? DarkSideIcons { get; set; }
    public int? BackLightSideIcons { get; set; }
    public int? BackDarkSideIcons { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    public ICollection<CardPrinting> Printings { get; set; } = new List<CardPrinting>();
    public ICollection<CardCardType> CardTypes { get; set; } = new List<CardCardType>();
    public ICollection<CardSubType> CardSubTypes { get; set; } = new List<CardSubType>();
    public ICollection<CardTrait> CardTraits { get; set; } = new List<CardTrait>();
    public ICollection<CardKeyword> CardKeywords { get; set; } = new List<CardKeyword>();
    public ICollection<CardArena> CardArenas { get; set; } = new List<CardArena>();
    public ICollection<CardAspect> CardAspects { get; set; } = new List<CardAspect>();
}
