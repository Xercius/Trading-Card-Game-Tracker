using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
/// Records the last successful import timestamp for each importer key.
/// Used to support incremental (delta) synchronisation via the updatedAt filter.
/// </summary>
public sealed class ImportSyncHistory
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The importer key (e.g. "swu", "scryfall"). Unique per importer.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public required string ImporterKey { get; set; }

    /// <summary>
    /// The set/expansion code last imported (e.g. "SOR"). Empty string for importers that do not use sets.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SetCode { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp recorded immediately before the last successful apply run.
    /// Use this as the lower-bound for the next incremental import.
    /// </summary>
    public DateTimeOffset LastSyncedAt { get; set; }
}
