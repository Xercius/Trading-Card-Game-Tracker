using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace api.Models;

/// <summary>
/// Represents a unique Star Wars: Unlimited card (logical card identity across all printings).
/// One row per distinct title/subtitle combination within a primary expansion set.
/// </summary>
[Index(nameof(StrapiId), IsUnique = true)]
[Index(nameof(CardUid), IsUnique = true)]
[Index(nameof(SwuSetId), nameof(Title), nameof(Subtitle))]
public sealed class SwuCard
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The Strapi/SWU API numeric record ID. Used as a stable upsert key for incremental syncs.
    /// </summary>
    public int StrapiId { get; set; }

    /// <summary>
    /// Unique card identifier returned by the SWU API (e.g. "7b3c2f1a-0000-0000-0000-000000000000").
    /// </summary>
    [MaxLength(64)]
    public string? CardUid { get; set; }

    /// <summary>
    /// Title part of the card name (e.g. "Luke Skywalker", "Millennium Falcon").
    /// </summary>
    [Required]
    [MaxLength(256)]
    public required string Title { get; set; }

    /// <summary>
    /// Subtitle part of the card name (e.g. "Faithful Friend", "Piece of Junk").
    /// Null for cards without a subtitle.
    /// </summary>
    [MaxLength(256)]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Card type as returned by the SWU API (e.g. "Unit", "Event", "Upgrade", "Leader", "Base").
    /// </summary>
    [Required]
    [MaxLength(64)]
    public required string CardType { get; set; }

    /// <summary>
    /// Rules text or abilities text for the card. Null for cards with no text.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Arena where the card operates (e.g. "Ground", "Space"). Null for non-unit cards.
    /// </summary>
    [MaxLength(32)]
    public string? Arena { get; set; }

    /// <summary>
    /// Resource cost to play this card. Null for cards without a cost (e.g. tokens).
    /// </summary>
    public int? Cost { get; set; }

    /// <summary>
    /// Combat power / attack value. Null for non-unit cards.
    /// </summary>
    public int? Power { get; set; }

    /// <summary>
    /// Combat health / hit points. Null for non-unit cards.
    /// </summary>
    public int? Health { get; set; }

    /// <summary>
    /// Artist credit for the primary art. Null if not provided by the API.
    /// </summary>
    [MaxLength(128)]
    public string? Artist { get; set; }

    /// <summary>
    /// Pipe-delimited list of aspect icons (e.g. "Heroism|Command|Villainy").
    /// Null or empty if the card has no aspects.
    /// </summary>
    [MaxLength(256)]
    public string? Aspects { get; set; }

    /// <summary>
    /// Pipe-delimited list of traits (e.g. "Rebel|Jedi|Force Sensitive").
    /// Null or empty if the card has no traits.
    /// </summary>
    [MaxLength(512)]
    public string? Traits { get; set; }

    /// <summary>
    /// Pipe-delimited list of keyword abilities (e.g. "Sentinel|Overwhelm").
    /// Null or empty if the card has no keywords.
    /// </summary>
    [MaxLength(512)]
    public string? Keywords { get; set; }

    /// <summary>
    /// Foreign key to the primary <see cref="SwuSet"/> that introduced this card.
    /// </summary>
    public int SwuSetId { get; set; }

    /// <summary>
    /// Navigation property to the primary expansion set that introduced this card.
    /// </summary>
    public SwuSet SwuSet { get; set; } = null!;

    /// <summary>
    /// Foreign key to the base/canonical card when this is a variant (alternate art, hyperspace, etc.).
    /// Null for base cards.
    /// </summary>
    public int? BaseCardId { get; set; }

    /// <summary>
    /// Navigation property to the base card this variant was derived from.
    /// </summary>
    public SwuCard? BaseCard { get; set; }

    /// <summary>
    /// UTC timestamp as returned by the SWU API (<c>createdAt</c> field).
    /// </summary>
    public DateTimeOffset? ApiCreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp as returned by the SWU API (<c>updatedAt</c> field).
    /// Used as the lower-bound filter for incremental syncs.
    /// </summary>
    public DateTimeOffset? ApiUpdatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last time this row was written by the importer.
    /// </summary>
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <summary>
    /// Printings of this card (different set releases, finishes, etc.).
    /// </summary>
    public ICollection<SwuCardPrinting> Printings { get; set; } = new List<SwuCardPrinting>();

    /// <summary>
    /// Variant cards that point to this card as their base.
    /// </summary>
    public ICollection<SwuCard> Variants { get; set; } = new List<SwuCard>();
}
