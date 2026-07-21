using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace api.Models;

/// <summary>
/// Represents a specific printing (release variant) of a <see cref="SwuCard"/>.
/// One row per unique (SwuCardId, SwuSetId, Number, Style) combination.
/// </summary>
[Index(nameof(StrapiId), IsUnique = true)]
[Index(nameof(SwuCardId), nameof(SwuSetId), nameof(Number), nameof(Style), IsUnique = true)]
[Index(nameof(SwuSetId), nameof(Number))]
public sealed class SwuCardPrinting
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The Strapi/SWU API numeric record ID. Used as a stable upsert key for incremental syncs.
    /// </summary>
    public int StrapiId { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="SwuCard"/> this printing belongs to.
    /// </summary>
    public int SwuCardId { get; set; }

    /// <summary>
    /// Navigation property to the parent card.
    /// </summary>
    public SwuCard SwuCard { get; set; } = null!;

    /// <summary>
    /// Foreign key to the <see cref="SwuSet"/> this printing was released in.
    /// </summary>
    public int SwuSetId { get; set; }

    /// <summary>
    /// Navigation property to the expansion set this printing belongs to.
    /// </summary>
    public SwuSet SwuSet { get; set; } = null!;

    /// <summary>
    /// Collector/set number for this printing (e.g. "SOR-001", "001").
    /// Sourced from <c>serialCode</c> when present; falls back to <c>cardNumber</c>.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public required string Number { get; set; }

    /// <summary>
    /// Rarity of this printing (e.g. "Common", "Uncommon", "Rare", "Legendary").
    /// Falls back to "Unknown" when the API does not supply a value.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public required string Rarity { get; set; }

    /// <summary>
    /// Print style / finish (e.g. "Standard", "Foil", "Hyperspace", "Showcase").
    /// </summary>
    [Required]
    [MaxLength(64)]
    public required string Style { get; set; }

    /// <summary>
    /// URL to the front art image for this printing. Null if not provided by the API.
    /// </summary>
    [MaxLength(1024)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL to the back art image for double-sided cards. Null for single-sided printings.
    /// </summary>
    [MaxLength(1024)]
    public string? BackImageUrl { get; set; }

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
}
