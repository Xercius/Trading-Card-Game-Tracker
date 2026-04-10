using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Card
{
    [Key]
    public int Id { get; set; }
    public required string Game { get; set; } // Magic, Lorcana, Star Wars Unlimited etc..
    public required string Name { get; set; } // Name of card — maps to spreadsheet "title"
    public required string CardType { get; set; } // Unit, Instant, Sorcery, Upgrade, Enchantment etc.. — maps to spreadsheet "type"
    public string? Description { get; set; } // Optional rules text
    public string? DetailsJson { get; set; } // Source-specific payload

    // --- Collection Tracker spreadsheet fields ---
    /// <summary>Spreadsheet jasonsCardId — unique integer identifier assigned by the tracker. Null for cards imported from external sources that do not carry this identifier.</summary>
    public int? JasonsCardId { get; set; }

    /// <summary>Spreadsheet subtitle — secondary name line (e.g. "Hope of the Rebellion"). Nullable.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Spreadsheet unique — whether the card is a unique/legendary unit.</summary>
    public bool Unique { get; set; }

    /// <summary>Spreadsheet cost — resource cost to play the card.</summary>
    public int Cost { get; set; }

    /// <summary>Spreadsheet hp — hit points / health.</summary>
    public int Hp { get; set; }

    /// <summary>Spreadsheet power — attack power.</summary>
    public int Power { get; set; }

    /// <summary>Spreadsheet upgradeHp — HP bonus granted when this card is an upgrade. Nullable.</summary>
    public int? UpgradeHp { get; set; }

    /// <summary>Spreadsheet upgradePower — Power bonus granted when this card is an upgrade. Nullable.</summary>
    public int? UpgradePower { get; set; }

    /// <summary>Spreadsheet type2 — secondary type classification. Nullable.</summary>
    public string? Type2 { get; set; }

    /// <summary>Spreadsheet arena — gameplay arena (e.g. "Ground", "Space"). Required.</summary>
    public required string Arena { get; set; }

    // Multi-value taxonomy fields stored as JSON TEXT arrays.
    // Rationale: no existing lookup/join tables exist for these fields; using JSON TEXT
    // columns is consistent with the existing DetailsJson pattern and avoids introducing
    // several new tables and join entities for fields that are currently read-only taxonomy.
    // If normalization is needed in a future iteration, these columns can be migrated to
    // proper join tables following the DeckCard/UserCard pattern.

    /// <summary>Spreadsheet aspects — JSON array of aspect strings (e.g. ["Villainy","Aggression"]).</summary>
    public string? AspectsJson { get; set; }

    /// <summary>Spreadsheet traits — JSON array of trait strings (e.g. ["Bounty Hunter","Mandalorian"]).</summary>
    public string? TraitsJson { get; set; }

    /// <summary>Spreadsheet keywords — JSON array of keyword strings (e.g. ["Overwhelm","Raid 2"]).</summary>
    public string? KeywordsJson { get; set; }

    /// <summary>Spreadsheet aspectDuplicates — nullable JSON array of duplicated aspect tokens.</summary>
    public string? AspectDuplicatesJson { get; set; }

    public ICollection<CardPrinting> Printings { get; set; } = new List<CardPrinting>();
}
