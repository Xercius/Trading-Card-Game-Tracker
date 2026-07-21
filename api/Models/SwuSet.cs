using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace api.Models;

/// <summary>
/// Represents a Star Wars: Unlimited expansion set (e.g. "SOR" – Spark of Rebellion).
/// One row is upserted per expansion code during SWU imports.
/// </summary>
[Index(nameof(Code), IsUnique = true)]
public sealed class SwuSet
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Uppercase expansion code as returned by the SWU API (e.g. "SOR", "SHD", "TWI").
    /// </summary>
    [Required]
    [MaxLength(32)]
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable expansion name (e.g. "Spark of Rebellion").
    /// </summary>
    [MaxLength(256)]
    public string? Name { get; set; }

    /// <summary>
    /// UTC release date of the expansion, if known.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last time this set row was written by the importer.
    /// </summary>
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <summary>
    /// Cards that were introduced in this expansion set.
    /// </summary>
    public ICollection<SwuCard> Cards { get; set; } = new List<SwuCard>();

    /// <summary>
    /// All printings released as part of this expansion set.
    /// </summary>
    public ICollection<SwuCardPrinting> Printings { get; set; } = new List<SwuCardPrinting>();

    /// <summary>
    /// Sync log entries for import operations that targeted this set.
    /// </summary>
    public ICollection<SyncLog> SyncLogs { get; set; } = new List<SyncLog>();
}
